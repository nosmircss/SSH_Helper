using System.Net;
using System.Net.Sockets;
using FluentAssertions;
using SSH_Helper.Services;
using Xunit;

namespace SSH_Helper.Tests.Services;

public class ConnectionPortTestConnectionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TestConnection_ResolvesPolicyBeforeConnectingToActualCandidate(bool debug)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var openPort = ((IPEndPoint)listener.LocalEndpoint).Port;
        using var closed = new TcpListener(IPAddress.Loopback, 0);
        closed.Start();
        var closedPort = ((IPEndPoint)closed.LocalEndpoint).Port;
        closed.Stop();
        var host = ConnectionPortResolverTests.Host($"{closedPort},{openPort}");
        host.IpAddress = "127.0.0.1";
        var probe = new RecordingPortProbe { Status = p => p == openPort ? "open" : "closed" };
        using var service = new SshExecutionService { PortResolver = new(probe), DebugMode = debug };
        var eventHosts = new List<SSH_Helper.Models.HostConnection>();
        service.OutputReceived += (_, e) => eventHosts.Add(e.Host);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var result = await service.TestConnectionAsync(host, 1000, timeout.Token);
        result.Success.Should().BeTrue();
        using var accepted = await listener.AcceptTcpClientAsync(timeout.Token);
        probe.Ports.Should().Equal(closedPort, openPort).And.NotContain(22);
        if (debug)
            eventHosts.Should().NotBeEmpty().And.OnlyContain(h => ReferenceEquals(h, host));
        else
            eventHosts.Should().BeEmpty();
        host.Port.Should().Be(22);
        host.Variables["port"].Should().Be($"{closedPort},{openPort}");
    }

    [Fact]
    public async Task TestConnection_ExhaustionDoesNotAttemptUnlistedPort()
    {
        var host = ConnectionPortResolverTests.Host("222,2222");
        var probe = new RecordingPortProbe { Status = _ => "closed" };
        using var service = new SshExecutionService { PortResolver = new(probe) };
        var result = await service.TestConnectionAsync(host, 100, default);
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("222: closed").And.Contain("2222: closed");
        probe.Ports.Should().Equal(222, 2222);
    }

    [Fact]
    public async Task TestConnection_CancellationPreventsProbe()
    {
        var probe = new RecordingPortProbe();
        using var service = new SshExecutionService { PortResolver = new(probe) };
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var result = await service.TestConnectionAsync(ConnectionPortResolverTests.Host(), 100, cts.Token);
        result.ErrorCategory.Should().Be("Cancelled");
        probe.Ports.Should().BeEmpty();
    }
}
