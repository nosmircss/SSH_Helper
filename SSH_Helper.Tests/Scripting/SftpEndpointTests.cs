using FluentAssertions;
using System.Collections.Concurrent;
using SSH_Helper.Models;
using SSH_Helper.Services;
using SSH_Helper.Services.Scripting;
using SSH_Helper.Services.Scripting.Commands;
using SSH_Helper.Services.Scripting.Models;
using SSH_Helper.Tests.Services;
using Xunit;

namespace SSH_Helper.Tests.Scripting;

public class SftpEndpointTests
{
    [Fact]
    public async Task CalledSubroutineLazySftpKeepsCallerRuntimePortWithoutExportingLocals()
    {
        // SFTP resolves its default endpoint, then intentionally fails missing-username validation.
        // No real credentials, file transfer, or SSH transport are used.
        var localPath = typeof(SftpCommand).Assembly.Location.Replace("'", "''");
        var yaml = $$"""
            subroutines:
              transfer:
                steps:
                  - set:
                      expression: ordinary = "child"
                  - sftp:
                      action: upload
                      local_path: '{{localPath}}'
                      remote_path: /unused
                      on_error: continue
            steps:
              - call:
                  subroutine: transfer
              - set:
                  expression: caller_port = "${port}"
            """;
        var host = ConnectionPortResolverTests.Host();
        var probe = new RecordingPortProbe();
        var context = new ScriptContext(host.Variables) { CurrentHost = host, PortResolver = new(probe) };
        var parser = new ScriptParser();
        var script = parser.Parse(yaml);
        parser.Validate(script, yaml, enforceCanonicalSyntax: true).Should().BeEmpty();
        var result = await new ScriptExecutor().ExecuteAsync(script, context);
        result.Status.Should().Be(ScriptExitStatus.Success, $"{result.Message}\n{result.FullOutput}");
        context.GetVariableString("caller_port").Should().Be("222");
        context.HasVariable("ordinary").Should().BeFalse();
        probe.Ports.Should().Equal(22, 222);
        host.Variables["port"].Should().Be("22,222,2222");
    }

    private sealed class BlockingProbe : IConnectionPortProbe
    {
        internal ConcurrentQueue<int> Attempts { get; } = new();
        internal TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource<ConnectionPortProbeResult> Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _attempts;

        public async Task<ConnectionPortProbeResult> CheckAsync(HostConnection host, int port, string mode,
            TimeSpan timeout, CancellationToken cancellationToken)
        {
            Attempts.Enqueue(port);
            if (Interlocked.Increment(ref _attempts) == 1)
            {
                Started.TrySetResult();
                return await Release.Task.WaitAsync(cancellationToken);
            }
            return new("open", "0", "");
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SimultaneousDefaultHostOperationsShareOneDiscovery(bool childScope)
    {
        var host = ConnectionPortResolverTests.Host("222,2222");
        var parent = new ScriptContext(host.Variables) { CurrentHost = host };
        var secondContext = childScope ? parent.CreateChildScope() : parent;
        var probe = new BlockingProbe();
        var resolver = new ConnectionPortResolver(probe);
        var first = SftpCommand.ResolveEndpointAsync(new(), parent, resolver, default);
        await probe.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var second = SftpCommand.ResolveEndpointAsync(new(), secondContext, resolver, default);
        probe.Release.SetResult(new("closed", "0", "test closed"));
        var endpoints = await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(5));

        endpoints.Select(e => e.Port).Should().Equal(2222, 2222);
        probe.Attempts.Should().Equal(222, 2222);
        parent.CurrentHost.Should().BeSameAs(secondContext.CurrentHost);
        parent.GetVariableString("port").Should().Be("2222");
        host.Variables["port"].Should().Be("222,2222");
    }

    [Fact]
    public async Task SimultaneousFailureIsSharedAndDoesNotPublishOrRetryWithinRun()
    {
        var host = ConnectionPortResolverTests.Host("222,2222");
        var parent = new ScriptContext(host.Variables) { CurrentHost = host };
        var probe = new BlockingProbe();
        var resolver = new ConnectionPortResolver(probe);
        var first = SftpCommand.ResolveEndpointAsync(new(), parent, resolver, default);
        await probe.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var second = SftpCommand.ResolveEndpointAsync(new(), parent.CreateChildScope(), resolver, default);
        probe.Release.SetException(new InvalidOperationException("test configuration failure"));

        await ((Func<Task>)(async () => await first)).Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("test configuration failure");
        await ((Func<Task>)(async () => await second)).Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("test configuration failure");
        var again = () => SftpCommand.ResolveEndpointAsync(new(), parent, resolver, default);
        await again.Should().ThrowAsync<InvalidOperationException>();
        probe.Attempts.Should().Equal(222);
        parent.CurrentHost.Should().BeSameAs(host);
        parent.GetVariableString("port").Should().Be("222,2222");

        var nextRun = new ScriptContext(host.Variables) { CurrentHost = host };
        var next = await SftpCommand.ResolveEndpointAsync(new(), nextRun, resolver, default);
        next.Port.Should().Be(222);
        probe.Attempts.Should().Equal(222, 222);
    }

    [Fact]
    public async Task CancelledWaiterDoesNotCancelSharedDiscovery()
    {
        var host = ConnectionPortResolverTests.Host("222,2222");
        var parent = new ScriptContext(host.Variables) { CurrentHost = host };
        var probe = new BlockingProbe();
        var resolver = new ConnectionPortResolver(probe);
        var first = SftpCommand.ResolveEndpointAsync(new(), parent, resolver, default);
        await probe.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        using var waiting = new CancellationTokenSource();
        var second = SftpCommand.ResolveEndpointAsync(new(), parent.CreateChildScope(), resolver, waiting.Token);
        waiting.Cancel();
        try
        {
            await ((Func<Task>)(async () => await second)).Should().ThrowAsync<OperationCanceledException>();
        }
        finally { probe.Release.TrySetResult(new("open", "0", "")); }
        (await first).Port.Should().Be(222);
        probe.Attempts.Should().Equal(222);
    }

    [Fact]
    public async Task OwnerCancellationCancelsSharedAttemptWithoutPublishingAndNewRunCanRetry()
    {
        var host = ConnectionPortResolverTests.Host("222,2222");
        var parent = new ScriptContext(host.Variables) { CurrentHost = host };
        var probe = new BlockingProbe();
        var resolver = new ConnectionPortResolver(probe);
        using var run = new CancellationTokenSource();
        var first = SftpCommand.ResolveEndpointAsync(new(), parent, resolver, run.Token);
        await probe.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var second = SftpCommand.ResolveEndpointAsync(new(), parent.CreateChildScope(), resolver, default);
        run.Cancel();
        await ((Func<Task>)(async () => await first)).Should().ThrowAsync<OperationCanceledException>();
        await ((Func<Task>)(async () => await second)).Should().ThrowAsync<OperationCanceledException>();
        probe.Attempts.Should().Equal(222);
        parent.CurrentHost.Should().BeSameAs(host);
        parent.GetVariableString("port").Should().Be("222,2222");
        var nextRun = new ScriptContext(host.Variables) { CurrentHost = host };
        (await SftpCommand.ResolveEndpointAsync(new(), nextRun, resolver, default)).Port.Should().Be(222);
        probe.Attempts.Should().Equal(222, 222);
    }

    [Fact]
    public async Task ChildScopeLazyResolutionPublishesRuntimePortButNotOrdinaryLocals()
    {
        var host = ConnectionPortResolverTests.Host();
        var parent = new ScriptContext(host.Variables) { CurrentHost = host };
        parent.SetVariable("ordinary", "caller");
        var child = parent.CreateChildScope(new Dictionary<string, object?>
        {
            ["port"] = host.PortPolicy!.RawPort, ["ordinary"] = "child"
        });
        var sibling = parent.CreateChildScope(new Dictionary<string, object?> { ["port"] = "stale" });
        var probe = new RecordingPortProbe();
        var endpoint = await SftpCommand.ResolveEndpointAsync(new(), child, new(probe), default);

        endpoint.Port.Should().Be(222);
        parent.GetVariableString("port").Should().Be("222");
        parent.SubstituteVariables("${port} / {{port}}").Should().Be("222 / 222");
        sibling.GetVariableString("port").Should().Be("222");
        parent.GetAllVariables()["port"].Should().Be("222");
        var laterChild = parent.CreateChildScope();
        laterChild.HasVariable("port").Should().BeTrue();
        laterChild.GetVariableString("port").Should().Be("222");
        child.GetVariableString("ordinary").Should().Be("child");
        parent.GetVariableString("ordinary").Should().Be("caller");
        laterChild.HasVariable("ordinary").Should().BeFalse();
        host.Variables["port"].Should().Be("22,222,2222");
    }

    [Fact]
    public async Task DefaultHostResolvesOnceAndSeedsActualPort()
    {
        var host = ConnectionPortResolverTests.Host();
        var context = new ScriptContext(host.Variables) { CurrentHost = host };
        var probe = new RecordingPortProbe();
        var resolver = new ConnectionPortResolver(probe);
        var first = await SftpCommand.ResolveEndpointAsync(new(), context, resolver, default);
        var second = await SftpCommand.ResolveEndpointAsync(new(), context, resolver, default);
        first.Host.Should().Be("router");
        first.Port.Should().Be(222);
        second.Port.Should().Be(222);
        probe.Ports.Should().Equal(22, 222);
        context.GetVariableString("port").Should().Be("222");
        host.Variables["port"].Should().Be("22,222,2222");
    }

    [Theory]
    [InlineData("alternate", null, 22)]
    [InlineData("alternate:2200", null, 2200)]
    [InlineData("alternate:2200", 2201, 2201)]
    [InlineData(null, 2201, 2201)]
    public async Task ExplicitEndpointBypassesCurrentHostPolicy(string? address, int? port, int expected)
    {
        var host = ConnectionPortResolverTests.Host("bad,list");
        var context = new ScriptContext(new() { ["Host_IP"] = "router" }) { CurrentHost = host };
        var probe = new RecordingPortProbe();
        var result = await SftpCommand.ResolveEndpointAsync(new() { Host = address, Port = port },
            context, new(probe), default);
        result.Port.Should().Be(expected);
        probe.Ports.Should().BeEmpty();
    }

    [Fact]
    public async Task ResolvedPreconnectEndpointIsReusedWithoutProbe()
    {
        var probe = new RecordingPortProbe();
        var resolver = new ConnectionPortResolver(probe);
        var resolved = await resolver.ResolveAsync(ConnectionPortResolverTests.Host("bad,list"),
            "2200", _ => { }, default);
        var context = new ScriptContext(resolved.Variables) { CurrentHost = resolved };
        var result = await SftpCommand.ResolveEndpointAsync(new(), context, resolver, default);
        result.Port.Should().Be(2200);
        probe.Ports.Should().BeEmpty();
    }
}
