using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using SSH_Helper.Services.Scripting;
using SSH_Helper.Services.Scripting.Commands;
using SSH_Helper.Services.Scripting.Models;
using Xunit;

namespace SSH_Helper.Tests.Scripting;

public class NetworkCommandTests
{
    [Fact]
    public async Task HttpCommand_Non2xx_AllowFailureTrue_DoesNotFailStep()
    {
        var command = new HttpCommand(_ => new StubHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("server-error")
            })));

        var step = new ScriptStep
        {
            Http = new HttpOptions
            {
                Url = "https://example.test/api",
                AllowFailure = true,
                Into = "http_result"
            }
        };

        var context = new ScriptContext();
        var result = await command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.SuppressedError.Should().BeFalse();
        context.GetVariableString("http_result").Should().Be("server-error");
        context.GetVariableString("http_result_status").Should().Be("500");
        long.Parse(context.GetVariableString("http_result_api_ms")).Should().BeGreaterThanOrEqualTo(0);
        long.Parse(context.GetVariableString("http_result_total_ms")).Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task HttpCommand_Success_CapturesApiAndTotalTimingVariables()
    {
        var command = new HttpCommand(_ => new StubHttpMessageHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(15, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("ok")
            };
        }));

        var step = new ScriptStep
        {
            Http = new HttpOptions
            {
                Url = "https://example.test/api",
                Into = "http_result"
            }
        };

        var context = new ScriptContext();
        var result = await command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeTrue();
        var apiMs = long.Parse(context.GetVariableString("http_result_api_ms"));
        var totalMs = long.Parse(context.GetVariableString("http_result_total_ms"));
        apiMs.Should().BeGreaterThanOrEqualTo(0);
        totalMs.Should().BeGreaterThanOrEqualTo(0);
        totalMs.Should().BeGreaterThanOrEqualTo(apiMs);
    }

    [Fact]
    public async Task HttpCommand_Non2xx_AllowFailureFalse_UsesOnErrorContinue()
    {
        var command = new HttpCommand(_ => new StubHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("bad-request")
            })));

        var step = new ScriptStep
        {
            Http = new HttpOptions
            {
                Url = "https://example.test/api",
                AllowFailure = false,
                Into = "http_result"
            },
            OnError = "continue"
        };

        var context = new ScriptContext();
        var result = await command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.SuppressedError.Should().BeTrue();
        context.GetVariableString("http_result").Should().Be("bad-request");
        context.GetVariableString("http_result_status").Should().Be("400");
    }

    [Fact]
    public async Task HttpCommand_TransportFailure_ClearsStaleIntoValues()
    {
        var command = new HttpCommand(_ => new StubHttpMessageHandler((_, _) =>
            throw new HttpRequestException("Simulated transport failure")));

        var step = new ScriptStep
        {
            Http = new HttpOptions
            {
                Url = "https://example.test/fail",
                Into = "http_result"
            },
            OnError = "continue"
        };

        var context = new ScriptContext();
        context.SetVariable("http_result", "stale-body");
        context.SetVariable("http_result_status", 200);
        context.SetVariable("http_result_headers", "{\"stale\":true}");
        context.SetVariable("http_result_api_ms", 999);
        context.SetVariable("http_result_total_ms", 999);

        var result = await command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.SuppressedError.Should().BeTrue();
        context.GetVariableString("http_result").Should().BeEmpty();
        context.GetVariableString("http_result_status").Should().BeEmpty();
        context.GetVariableString("http_result_headers").Should().BeEmpty();
        context.GetVariableString("http_result_api_ms").Should().BeEmpty();
        context.GetVariableString("http_result_total_ms").Should().BeEmpty();
    }

    [Fact]
    public async Task HttpCommand_ContentTypeHeader_TakesPrecedenceOverShorthand()
    {
        string? capturedContentType = null;
        var command = new HttpCommand(_ => new StubHttpMessageHandler((request, _) =>
        {
            capturedContentType = request.Content?.Headers.ContentType?.MediaType;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("ok")
            });
        }));

        var step = new ScriptStep
        {
            Http = new HttpOptions
            {
                Url = "https://example.test/content",
                Method = "POST",
                Body = "{\"k\":1}",
                ContentType = "json",
                Headers = new Dictionary<string, string>
                {
                    ["Content-Type"] = "text/plain"
                }
            }
        };

        var context = new ScriptContext();
        var result = await command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeTrue();
        capturedContentType.Should().Be("text/plain");
    }

    [Fact]
    public async Task HttpCommand_ContentTypeShorthand_MapsToMimeType()
    {
        string? capturedContentType = null;
        var command = new HttpCommand(_ => new StubHttpMessageHandler((request, _) =>
        {
            capturedContentType = request.Content?.Headers.ContentType?.MediaType;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("ok")
            });
        }));

        var step = new ScriptStep
        {
            Http = new HttpOptions
            {
                Url = "https://example.test/content",
                Method = "POST",
                Body = "k=v",
                ContentType = "FoRm"
            }
        };

        var context = new ScriptContext();
        var result = await command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeTrue();
        capturedContentType.Should().Be("application/x-www-form-urlencoded");
    }

    [Fact]
    public async Task HttpCommand_VerifyTls_DefaultValidationAndOptOutBehavior()
    {
        var command = new HttpCommand(options =>
        {
            if (options.VerifyTls)
            {
                return new StubHttpMessageHandler((_, _) =>
                    throw new HttpRequestException("TLS certificate validation failed"));
            }

            return new StubHttpMessageHandler((_, _) =>
                Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("ok-no-verify")
                }));
        });

        var stepWithDefaultTls = new ScriptStep
        {
            Http = new HttpOptions
            {
                Url = "https://localhost/test",
                Into = "tls_result"
            }
        };

        var stepWithTlsDisabled = new ScriptStep
        {
            Http = new HttpOptions
            {
                Url = "https://localhost/test",
                VerifyTls = false,
                Into = "tls_result2"
            }
        };

        var context = new ScriptContext();
        var failResult = await command.ExecuteAsync(stepWithDefaultTls, context, CancellationToken.None);
        var passResult = await command.ExecuteAsync(stepWithTlsDisabled, context, CancellationToken.None);

        failResult.Success.Should().BeFalse();
        context.GetVariableString("tls_result").Should().BeEmpty();
        passResult.Success.Should().BeTrue();
        context.GetVariableString("tls_result2").Should().Be("ok-no-verify");
    }

    [Fact]
    public async Task DnsCommand_CapturesListCountAndSupportsIndexedVariableAccess()
    {
        var resolver = new FakeDnsResolver
        {
            Addresses = new[]
            {
                IPAddress.Parse("1.1.1.1"),
                IPAddress.Parse("1.0.0.1")
            }
        };
        var command = new DnsCommand(resolver);

        var step = new ScriptStep
        {
            Dns = new DnsOptions
            {
                Host = "example.com",
                Type = "A",
                Into = "resolved"
            }
        };

        var context = new ScriptContext();
        var result = await command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeTrue();
        context.GetVariable("resolved").Should().BeAssignableTo<List<string>>();
        var values = (List<string>)context.GetVariable("resolved")!;
        values.Should().Equal("1.1.1.1", "1.0.0.1");
        context.GetVariableString("resolved_count").Should().Be("2");
        context.SubstituteVariables("${resolved[1]}").Should().Be("1.0.0.1");
    }

    [Fact]
    public async Task DnsCommand_NoRecords_ReturnsEmptyListAndZeroCount()
    {
        var resolver = new FakeDnsResolver { Addresses = Array.Empty<IPAddress>() };
        var command = new DnsCommand(resolver);

        var step = new ScriptStep
        {
            Dns = new DnsOptions
            {
                Host = "empty.test",
                Type = "A",
                Into = "resolved"
            }
        };

        var context = new ScriptContext();
        var result = await command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeTrue();
        ((List<string>)context.GetVariable("resolved")!).Should().BeEmpty();
        context.GetVariableString("resolved_count").Should().Be("0");
    }

    [Fact]
    public async Task DnsCommand_FailurePath_ClearsStaleIntoValues()
    {
        var resolver = new FakeDnsResolver
        {
            Exception = new SocketException((int)SocketError.TimedOut)
        };
        var command = new DnsCommand(resolver);

        var step = new ScriptStep
        {
            Dns = new DnsOptions
            {
                Host = "broken.test",
                Type = "A",
                Into = "resolved"
            },
            OnError = "continue"
        };

        var context = new ScriptContext();
        context.SetVariable("resolved", new List<string> { "stale" });
        context.SetVariable("resolved_count", 1);

        var result = await command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.SuppressedError.Should().BeTrue();
        ((List<string>)context.GetVariable("resolved")!).Should().BeEmpty();
        context.GetVariableString("resolved_count").Should().Be("0");
    }

    [Fact]
    public async Task PingCommand_CompleteFailure_SetsFailureMetrics()
    {
        var probe = new FakePingProbe(
            new PingCommand.PingProbeResult(IPStatus.TimedOut, 0),
            new PingCommand.PingProbeResult(IPStatus.TimedOut, 0),
            new PingCommand.PingProbeResult(IPStatus.TimedOut, 0));
        var command = new PingCommand(probe);

        var step = new ScriptStep
        {
            Ping = new SSH_Helper.Services.Scripting.Models.PingOptions
            {
                Host = "example.test",
                Count = 3,
                Timeout = 10,
                Into = "ping_result"
            },
            OnError = "continue"
        };

        var context = new ScriptContext();
        var result = await command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.SuppressedError.Should().BeTrue();
        context.GetVariableString("ping_result").Should().Be("failure");
        context.GetVariableString("ping_result_avg").Should().BeEmpty();
        context.GetVariableString("ping_result_loss").Should().Be("100");
    }

    [Fact]
    public async Task PingCommand_PartialSuccess_CapturesAvgAndLoss()
    {
        var probe = new FakePingProbe(
            new PingCommand.PingProbeResult(IPStatus.Success, 10),
            new PingCommand.PingProbeResult(IPStatus.TimedOut, 0),
            new PingCommand.PingProbeResult(IPStatus.Success, 30));
        var command = new PingCommand(probe);

        var step = new ScriptStep
        {
            Ping = new SSH_Helper.Services.Scripting.Models.PingOptions
            {
                Host = "example.test",
                Count = 3,
                Timeout = 10,
                Into = "ping_result"
            }
        };

        var context = new ScriptContext();
        var result = await command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeTrue();
        context.GetVariableString("ping_result").Should().Be("success");
        context.GetVariableString("ping_result_avg").Should().Be("20");
        context.GetVariableString("ping_result_loss").Should().Be("33");
    }

    [Fact]
    public async Task PortcheckCommand_OpenPort_SetsOpenStatus()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        var command = new PortcheckCommand();
        var step = new ScriptStep
        {
            Portcheck = new PortcheckOptions
            {
                Host = "127.0.0.1",
                Port = port,
                Timeout = 2,
                Into = "port_state"
            }
        };

        var context = new ScriptContext();
        var result = await command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeTrue();
        context.GetVariableString("port_state").Should().Be("open");
        context.GetVariableString("port_state_latency").Should().NotBeEmpty();
    }

    [Fact]
    public async Task PortcheckCommand_ClosedPort_SetsNonOpenStatus()
    {
        int port;
        using (var listener = new TcpListener(IPAddress.Loopback, 0))
        {
            listener.Start();
            port = ((IPEndPoint)listener.LocalEndpoint).Port;
        }

        var command = new PortcheckCommand();
        var step = new ScriptStep
        {
            Portcheck = new PortcheckOptions
            {
                Host = "127.0.0.1",
                Port = port,
                Timeout = 1,
                Into = "port_state"
            },
            OnError = "continue"
        };

        var context = new ScriptContext();
        var result = await command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.SuppressedError.Should().BeTrue();
        context.GetVariableString("port_state").Should().BeOneOf("closed", "timeout");
    }

    [Fact]
    public async Task SftpCommand_OverwriteFalse_DownloadExistingDestinationFailsPredictably()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"sftp_cmd_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var existingLocalPath = Path.Combine(tempDir, "existing.txt");
            await File.WriteAllTextAsync(existingLocalPath, "already-there");

            var command = new SftpCommand();
            var step = new ScriptStep
            {
                Sftp = new SftpOptions
                {
                    Action = "DoWnLoAd",
                    LocalPath = existingLocalPath,
                    RemotePath = "/tmp/remote.txt",
                    Overwrite = false,
                    Into = "transfer"
                }
            };

            var context = new ScriptContext();
            var result = await command.ExecuteAsync(step, context, CancellationToken.None);

            result.Success.Should().BeFalse();
            result.Message.Should().Contain("already exists");
            context.GetVariableString("transfer").Should().Be("failure");
            context.GetVariableString("transfer_bytes").Should().Be("0");
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task WebhookCommand_Regression_CaptureBehaviorRemainsUnchanged()
    {
        using var server = new OneShotHttpServer(200, "{\"ok\":true}");
        var command = new WebhookCommand();
        var step = new ScriptStep
        {
            Webhook = new WebhookOptions
            {
                Url = server.Url,
                Method = "GET",
                Timeout = 5,
                Into = "webhook_result"
            }
        };

        var context = new ScriptContext();
        var result = await command.ExecuteAsync(step, context, CancellationToken.None);
        await server.Completion.WaitAsync(TimeSpan.FromSeconds(5));

        result.Success.Should().BeTrue();
        context.GetVariableString("webhook_result").Should().Be("{\"ok\":true}");
        context.GetVariableString("webhook_result_status").Should().Be("200");
    }

    [Fact]
    public async Task WebhookCommand_TransportFailure_ClearsStaleIntoValues()
    {
        var command = new WebhookCommand(() => new StubHttpMessageHandler((_, _) =>
            throw new HttpRequestException("Simulated transport failure")));
        var step = new ScriptStep
        {
            Webhook = new WebhookOptions
            {
                Url = "https://example.test/fail",
                Method = "GET",
                Timeout = 5,
                Into = "webhook_result"
            },
            OnError = "continue"
        };

        var context = new ScriptContext();
        context.SetVariable("webhook_result", "stale-body");
        context.SetVariable("webhook_result_status", 200);

        var result = await command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.SuppressedError.Should().BeTrue();
        context.GetVariableString("webhook_result").Should().BeEmpty();
        context.GetVariableString("webhook_result_status").Should().BeEmpty();
    }

    [Fact]
    public async Task ScriptExecutor_MixedWorkflow_SshAndNonSshSteps_ExecutesSuccessfully()
    {
        using var httpServer = new OneShotHttpServer(200, "integration-ok");
        using var portListener = new TcpListener(IPAddress.Loopback, 0);
        portListener.Start();
        var openPort = ((IPEndPoint)portListener.LocalEndpoint).Port;

        var script = new Script
        {
            Steps = new List<ScriptStep>
            {
                new()
                {
                    Send = "echo hello",
                    OnError = "continue"
                },
                new()
                {
                    Http = new HttpOptions
                    {
                        Url = httpServer.Url,
                        Timeout = 5,
                        Into = "http_result"
                    }
                },
                new()
                {
                    Portcheck = new PortcheckOptions
                    {
                        Host = "127.0.0.1",
                        Port = openPort,
                        Into = "port_state"
                    }
                }
            }
        };

        var executor = new ScriptExecutor();
        var context = new ScriptContext();

        var result = await executor.ExecuteAsync(script, context);
        await httpServer.Completion.WaitAsync(TimeSpan.FromSeconds(5));

        result.Status.Should().Be(ScriptExitStatus.Success);
        context.GetVariableString("http_result").Should().Be("integration-ok");
        context.GetVariableString("port_state").Should().Be("open");
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

        public StubHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return _handler(request, cancellationToken);
        }
    }

    private sealed class FakeDnsResolver : DnsCommand.IDnsResolver
    {
        public IPAddress[] Addresses { get; set; } = Array.Empty<IPAddress>();
        public IPHostEntry HostEntry { get; set; } = new() { HostName = string.Empty, AddressList = Array.Empty<IPAddress>(), Aliases = Array.Empty<string>() };
        public Exception? Exception { get; set; }

        public Task<IPAddress[]> GetHostAddressesAsync(string host, CancellationToken cancellationToken)
        {
            if (Exception != null)
                throw Exception;
            return Task.FromResult(Addresses);
        }

        public Task<IPHostEntry> GetHostEntryAsync(string host, CancellationToken cancellationToken)
        {
            if (Exception != null)
                throw Exception;
            return Task.FromResult(HostEntry);
        }
    }

    private sealed class FakePingProbe : PingCommand.IPingProbe
    {
        private readonly Queue<PingCommand.PingProbeResult> _results;

        public FakePingProbe(params PingCommand.PingProbeResult[] results)
        {
            _results = new Queue<PingCommand.PingProbeResult>(results);
        }

        public Task<PingCommand.PingProbeResult> SendAsync(string host, int timeoutMs)
        {
            if (_results.Count == 0)
                return Task.FromResult(new PingCommand.PingProbeResult(IPStatus.TimedOut, 0));

            return Task.FromResult(_results.Dequeue());
        }
    }

    private sealed class OneShotHttpServer : IDisposable
    {
        private readonly TcpListener _listener;
        private readonly Task _serveTask;
        private readonly int _statusCode;
        private readonly string _body;

        public OneShotHttpServer(int statusCode, string body)
        {
            _statusCode = statusCode;
            _body = body;
            _listener = new TcpListener(IPAddress.Loopback, 0);
            _listener.Start();
            var port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            Url = $"http://127.0.0.1:{port}/";
            _serveTask = ServeAsync();
        }

        public string Url { get; }

        public Task Completion => _serveTask;

        public void Dispose()
        {
            _listener.Stop();
        }

        private async Task ServeAsync()
        {
            try
            {
                using var client = await _listener.AcceptTcpClientAsync();
                await using var stream = client.GetStream();
                using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);

                // Read request line.
                _ = await reader.ReadLineAsync();

                // Read headers.
                var contentLength = 0;
                while (true)
                {
                    var line = await reader.ReadLineAsync();
                    if (line == null || line.Length == 0)
                        break;

                    const string contentLengthKey = "Content-Length:";
                    if (line.StartsWith(contentLengthKey, StringComparison.OrdinalIgnoreCase))
                    {
                        var raw = line[contentLengthKey.Length..].Trim();
                        int.TryParse(raw, out contentLength);
                    }
                }

                if (contentLength > 0)
                {
                    var buffer = new char[contentLength];
                    await reader.ReadBlockAsync(buffer, 0, buffer.Length);
                }

                var reason = _statusCode switch
                {
                    200 => "OK",
                    201 => "Created",
                    400 => "Bad Request",
                    404 => "Not Found",
                    500 => "Internal Server Error",
                    _ => "Response"
                };

                var bodyBytes = Encoding.UTF8.GetBytes(_body);
                var header = $"HTTP/1.1 {_statusCode} {reason}\r\nContent-Type: text/plain\r\nContent-Length: {bodyBytes.Length}\r\nConnection: close\r\n\r\n";
                var headerBytes = Encoding.ASCII.GetBytes(header);
                await stream.WriteAsync(headerBytes);
                await stream.WriteAsync(bodyBytes);
                await stream.FlushAsync();
            }
            catch (ObjectDisposedException)
            {
                // Listener was disposed before any client connected.
            }
            catch (IOException ex) when (ex.InnerException is SocketException)
            {
                // Client disconnected while response bytes were being written.
            }
            catch (SocketException)
            {
                // Listener was stopped while waiting for a client.
            }
        }
    }
}
