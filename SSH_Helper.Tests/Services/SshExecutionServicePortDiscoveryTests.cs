using FluentAssertions;
using SSH_Helper.Models;
using SSH_Helper.Services;
using Xunit;

namespace SSH_Helper.Tests.Services;

public class SshExecutionServicePortDiscoveryTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task SimpleAndYaml_ResolveBeforeTransportAndKeepOriginalEvents(bool pool, bool yaml)
    {
        using var service = new SshExecutionService(pool);
        var host = ConnectionPortResolverTests.Host();
        var probe = new RecordingPortProbe();
        service.PortResolver = new(probe);
        HostConnection? dispatched = null;
        var events = new List<HostConnection>();
        service.OutputReceived += (_, e) => events.Add(e.Host);
        service.ProgressChanged += (_, e) => events.Add(e.Host);
        service.BeforeConnectionForTesting = effective =>
        {
            probe.Ports.Should().Equal(22, 222);
            dispatched = effective;
            throw new InvalidOperationException("test transport boundary");
        };
        var results = yaml
            ? await service.ExecuteScriptAsync(new[] { host }, "steps:\n  - send: echo test", "", "", SshTimeoutOptions.Default)
            : await service.ExecuteAsync(new[] { host }, new[] { "echo test" }, "", "", SshTimeoutOptions.Default);
        results[0].Success.Should().BeFalse();
        results[0].ErrorMessage.Should().Contain("test transport boundary");
        results[0].Host.Should().BeSameAs(host);
        dispatched!.Port.Should().Be(222);
        dispatched.Variables["port"].Should().Be("222");
        events.Should().NotBeEmpty().And.OnlyContain(h => ReferenceEquals(h, host));
        host.Variables["port"].Should().Be("22,222,2222");
    }

    [Theory]
    [InlineData("  - set: _ssh_port = 2200", true)]
    [InlineData("  - exit: failure", false)]
    public async Task Yaml_PreconnectOverrideOrFailureBypassesInvalidPolicy(string preconnect, bool dispatched)
    {
        using var service = new SshExecutionService();
        var probe = new RecordingPortProbe();
        service.PortResolver = new(probe);
        var calls = 0;
        service.BeforeConnectionForTesting = host =>
        {
            calls++;
            host.Port.Should().Be(2200);
            throw new InvalidOperationException("boundary");
        };
        await service.ExecuteScriptAsync(new[] { ConnectionPortResolverTests.Host("bad,list") },
            $"preconnect:\n{preconnect}\nsteps:\n  - send: echo test", "", "", SshTimeoutOptions.Default);
        calls.Should().Be(dispatched ? 1 : 0);
        probe.Ports.Should().BeEmpty();
    }

    [Fact]
    public async Task Yaml_LocalOnlyDoesNotDiscover()
    {
        using var service = new SshExecutionService();
        var probe = new RecordingPortProbe();
        service.PortResolver = new(probe);
        var results = await service.ExecuteScriptAsync(new[] { ConnectionPortResolverTests.Host("bad,list") },
            "steps:\n  - print: local", "", "", SshTimeoutOptions.Default);
        results[0].Success.Should().BeTrue();
        probe.Ports.Should().BeEmpty();
    }

    [Fact]
    public async Task PreconnectSeesRawPolicyAndReportsBeforeDiscovery()
    {
        using var service = new SshExecutionService();
        var probe = new RecordingPortProbe();
        service.PortResolver = new(probe);
        var trace = new List<string>();
        service.OutputReceived += (_, e) => trace.Add(e.Output);
        probe.Attempt = port =>
        {
            trace.Should().Contain(m => m.Contains("raw=22,222,2222"));
            trace.Add($"probe {port}");
        };
        service.BeforeConnectionForTesting = _ => throw new InvalidOperationException("boundary");
        var results = await service.ExecuteScriptAsync(new[] { ConnectionPortResolverTests.Host() },
            "preconnect:\n  - print: 'raw=${port}'\nsteps:\n  - send: echo test", "", "", SshTimeoutOptions.Default);
        results[0].ErrorMessage.Should().Be("boundary");
        probe.Ports.Should().Equal(22, 222);
    }

    [Fact]
    public async Task AuthenticationFailureDoesNotAdvanceAndNewRunRestartsOrder()
    {
        using var service = new SshExecutionService(true);
        var probe = new RecordingPortProbe { Status = _ => "open" };
        service.PortResolver = new(probe);
        var host = ConnectionPortResolverTests.Host();
        service.BeforeConnectionForTesting = _ => throw new Rebex.Net.SshException("Authentication failed");
        var first = await service.ExecuteAsync(new[] { host }, new[] { "echo test" }, "", "", SshTimeoutOptions.Default);
        first[0].Success.Should().BeFalse();
        first[0].ErrorMessage.Should().Be("Authentication failed");
        probe.Ports.Should().Equal(22);
        probe.Status = p => p == 222 ? "open" : "closed";
        var dispatched = new List<int>();
        service.BeforeConnectionForTesting = effective =>
        {
            dispatched.Add(effective.Port);
            throw new InvalidOperationException("boundary");
        };
        await service.ExecuteAsync(new[] { host }, new[] { "echo test" }, "", "", SshTimeoutOptions.Default);
        probe.Status = _ => "open";
        await service.ExecuteAsync(new[] { host }, new[] { "echo test" }, "", "", SshTimeoutOptions.Default);
        probe.Ports.Should().Equal(22, 22, 222, 22);
        dispatched.Should().Equal(222, 22);
        host.Variables["port"].Should().Be("22,222,2222");
    }

    [Fact]
    public async Task ConcurrentSameAddressRowsKeepIndependentPoliciesAndAllEventIdentities()
    {
        using var service = new SshExecutionService { DebugMode = true };
        service.PortResolver = new(new RecordingPortProbe { Status = _ => "open" });
        var first = ConnectionPortResolverTests.Host("222,22");
        var second = ConnectionPortResolverTests.Host("2200,2222");
        var outputs = new System.Collections.Concurrent.ConcurrentBag<(HostConnection Host, string Text)>();
        var commands = new System.Collections.Concurrent.ConcurrentBag<HostConnection>();
        service.OutputReceived += (_, e) => outputs.Add((e.Host, e.Output));
        service.CommandCompleted += (_, e) => commands.Add(e.Host);
        service.BeforeConnectionForTesting = effective =>
        {
            typeof(SshExecutionService).GetMethod("OnCommandCompleted",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .Invoke(service, new object[] { effective, "test" });
            throw new InvalidOperationException("boundary");
        };
        var results = await service.ExecuteAsync(new[] { first, second }, new[] { "echo test" },
            "", "", SshTimeoutOptions.Default);
        results.Select(r => r.Host).Should().BeEquivalentTo(new[] { first, second });
        commands.Should().Contain(h => ReferenceEquals(h, first)).And.Contain(h => ReferenceEquals(h, second));
        outputs.Where(o => o.Text.Contains("Selected SSH")).Should().Contain(
            o => ReferenceEquals(o.Host, first) && o.Text.Contains(":222"));
        outputs.Where(o => o.Text.Contains("Selected SSH")).Should().Contain(
            o => ReferenceEquals(o.Host, second) && o.Text.Contains(":2200"));
        first.Variables["port"].Should().Be("222,22");
        second.Variables["port"].Should().Be("2200,2222");
    }

    [Fact]
    public async Task PreconnectCancellationStopsBeforeDiscoveryOrDispatch()
    {
        using var service = new SshExecutionService();
        var probe = new RecordingPortProbe();
        service.PortResolver = new(probe);
        var calls = 0;
        service.BeforeConnectionForTesting = _ => calls++;
        service.OutputReceived += (_, e) =>
        {
            if (e.Output.Contains("cancel-here")) service.Stop();
        };
        var results = await service.ExecuteScriptAsync(new[] { ConnectionPortResolverTests.Host() },
            "preconnect:\n  - print: cancel-here\n  - wait: 1\nsteps:\n  - send: echo test",
            "", "", SshTimeoutOptions.Default);
        results[0].Success.Should().BeFalse();
        probe.Ports.Should().BeEmpty();
        calls.Should().Be(0);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task PreconnectOverrideReportsSelectionOnlyInDebugMode(bool serviceDebug, bool scriptDebug)
    {
        using var service = new SshExecutionService { DebugMode = serviceDebug };
        service.PortResolver = new(new RecordingPortProbe());
        var result = await service.ExecuteScriptAsync(new[] { ConnectionPortResolverTests.Host("bad,list") },
            $"debug: {scriptDebug.ToString().ToLowerInvariant()}\nnobanner: true\npreconnect:\n  - set: _ssh_port = 2200\nsteps:\n  - print: done",
            "", "", SshTimeoutOptions.Default);
        result[0].Success.Should().BeTrue();
        if (serviceDebug || scriptDebug)
            result[0].Output.Should().Contain("Selected SSH endpoint router:2200");
        else
            result[0].Output.Should().NotContain("Selected SSH endpoint");
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, false)]
    [InlineData(true, false, true)]
    public async Task DiscoveryOutputIsDebugOnlyAndFailuresRemainVisible(bool yaml, bool serviceDebug, bool scriptDebug)
    {
        using var service = new SshExecutionService { DebugMode = serviceDebug };
        service.PortResolver = new(new RecordingPortProbe());
        var output = new List<string>();
        service.OutputReceived += (_, e) => output.Add(e.Output);
        service.BeforeConnectionForTesting = _ => throw new InvalidOperationException("test transport boundary");
        var host = ConnectionPortResolverTests.Host();
        var results = yaml
            ? await service.ExecuteScriptAsync(new[] { host },
                $"debug: {scriptDebug.ToString().ToLowerInvariant()}\nsteps:\n  - send: echo test",
                "", "", SshTimeoutOptions.Default)
            : await service.ExecuteAsync(new[] { host }, new[] { "echo test" }, "", "", SshTimeoutOptions.Default);
        var text = string.Concat(output);
        text.Should().Contain("test transport boundary");
        if (serviceDebug || scriptDebug)
        {
            text.Should().Contain("router:22: closed").And.Contain("Selected SSH endpoint router:222");
            results[0].Output.Should().Contain("Selected SSH endpoint router:222");
        }
        else
        {
            text.Should().NotContain("router:22: closed").And.NotContain("Selected SSH endpoint");
            results[0].Output.Should().NotContain("Selected SSH endpoint");
        }
    }
}
