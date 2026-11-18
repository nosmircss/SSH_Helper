using FluentAssertions;
using SSH_Helper.Models;
using SSH_Helper.Services;
using SSH_Helper.Services.Scripting.Models;
using Xunit;

namespace SSH_Helper.Tests.Services;

internal sealed class RecordingPortProbe : IConnectionPortProbe
{
    internal List<int> Ports { get; } = new();
    internal Func<int, string> Status { get; set; } = p => p == 222 ? "open" : "closed";
    internal Action<int>? Attempt { get; set; }
    internal List<(string Mode, TimeSpan Timeout)> Options { get; } = new();
    public Task<ConnectionPortProbeResult> CheckAsync(HostConnection host, int port, string mode,
        TimeSpan timeout, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Ports.Add(port);
        Options.Add((mode, timeout));
        Attempt?.Invoke(port);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new ConnectionPortProbeResult(Status(port), "0", "test outcome"));
    }
}

public class ConnectionPortResolverTests
{
    internal static HostConnection Host(string raw = "22,222,2222") => new()
    {
        IpAddress = "router", PortPolicy = new(raw, ConnectionPortSource.Environment, null, null),
        Variables = new() { ["port"] = raw }
    };

    [Theory]
    [InlineData("closed")]
    [InlineData("timeout")]
    public async Task Resolve_FirstQualifyingCandidateWinsAndDoesNotMutateSource(string firstStatus)
    {
        var host = Host();
        var probe = new RecordingPortProbe { Status = p => p == 22 ? firstStatus : "open" };
        var messages = new List<string>();
        var resolved = await new ConnectionPortResolver(probe).ResolveAsync(host, null, messages.Add, default);
        probe.Ports.Should().Equal(22, 222);
        probe.Options.Should().OnlyContain(o => o.Mode == "ssh" && o.Timeout == TimeSpan.FromSeconds(2));
        resolved.Port.Should().Be(222);
        resolved.Variables["port"].Should().Be("222");
        host.Variables["port"].Should().Be("22,222,2222");
        host.Port.Should().Be(22);
        resolved.IsPortResolvedForRun.Should().BeTrue();
        messages.Should().Equal(
            $"router:22: {firstStatus} (test outcome)",
            "router:222: open (test outcome)",
            "Selected SSH endpoint router:222");
    }

    [Theory]
    [InlineData("22", null, 22)]
    [InlineData("bad,list", "2200", 2200)]
    public async Task Resolve_FixedAndOverrideIgnoreDiscoveryOptions(string raw, string? over, int expected)
    {
        var host = Host(raw);
        host.PortPolicy = host.PortPolicy! with { RawCheck = "invalid", RawTimeout = "bad" };
        var probe = new RecordingPortProbe();
        var result = await new ConnectionPortResolver(probe).ResolveAsync(host, over, _ => { }, default);
        result.Port.Should().Be(expected);
        probe.Ports.Should().BeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("bad")]
    [InlineData("65536")]
    public async Task Resolve_InvalidOverrideNeverFallsBack(string over)
    {
        var probe = new RecordingPortProbe();
        var action = () => new ConnectionPortResolver(probe).ResolveAsync(Host(), over, _ => { }, default);
        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("*_ssh_port*");
        probe.Ports.Should().BeEmpty();
    }

    [Fact]
    public async Task Resolve_WholeListValidatedBeforeFirstAttempt()
    {
        var probe = new RecordingPortProbe();
        var action = () => new ConnectionPortResolver(probe).ResolveAsync(Host("22,bad"), null, _ => { }, default);
        await action.Should().ThrowAsync<InvalidOperationException>();
        probe.Ports.Should().BeEmpty();
    }

    [Fact]
    public async Task Resolve_ExhaustedDoesNotFallBack()
    {
        var probe = new RecordingPortProbe { Status = p => p == 222 ? "closed" : "timeout" };
        var action = () => new ConnectionPortResolver(probe).ResolveAsync(Host("222,2222"), null, _ => { }, default);
        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("*222: closed*2222: timeout*");
        probe.Ports.Should().Equal(222, 2222);
    }

    [Fact]
    public async Task Resolve_CancellationStopsBeforeNextCandidate()
    {
        using var cts = new CancellationTokenSource();
        var probe = new RecordingPortProbe { Attempt = _ => cts.Cancel() };
        var action = () => new ConnectionPortResolver(probe).ResolveAsync(Host(), null, _ => { }, cts.Token);
        await action.Should().ThrowAsync<OperationCanceledException>();
        probe.Ports.Should().Equal(22);
        probe.Ports.Clear();
        await action.Should().ThrowAsync<OperationCanceledException>();
        probe.Ports.Should().BeEmpty();
    }

    [Fact]
    public async Task Resolve_EachNewRunStartsAtFirstCandidateButActiveHostIsReused()
    {
        var host = Host();
        var probe = new RecordingPortProbe();
        var resolver = new ConnectionPortResolver(probe);
        var first = await resolver.ResolveAsync(host, null, _ => { }, default);
        probe.Status = _ => "open";
        var second = await resolver.ResolveAsync(host, null, _ => { }, default);
        await resolver.ResolveAsync(first, null, _ => { }, default);
        probe.Ports.Should().Equal(22, 222, 22);
        second.Port.Should().Be(22);
        first.Variables.Should().NotBeSameAs(second.Variables);
        new PortcheckOptions().Mode.Should().Be("tcp");
        new PortcheckOptions().Timeout.Should().Be(5);
    }
}
