using FluentAssertions;
using SSH_Helper.Services.Scripting;
using SSH_Helper.Services.Scripting.Models;
using SSH_Helper.Services.Terminal;
using Xunit;

namespace SSH_Helper.Tests.Services;

public class InteractiveTerminalPortDiscoveryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CurrentHostDiscoveryReportsOnlyInDebugMode(bool debug)
    {
        var context = new ScriptContext
        {
            CurrentHost = ConnectionPortResolverTests.Host(),
            DebugMode = debug
        };
        var messages = new List<string>();
        context.OutputReceived += (_, e) => messages.Add(e.Message);
        var resolved = await context.ResolveCurrentHostAsync(new(new RecordingPortProbe()), default);
        resolved.Port.Should().Be(222);
        if (debug)
            messages.Should().Contain(m => m.Contains("Selected SSH endpoint"));
        else
            messages.Should().BeEmpty();
    }

    [Fact]
    public async Task CancellationAndSharedModeDoNotProbe()
    {
        var probe = new RecordingPortProbe();
        var service = new InteractiveTerminalService { PortResolver = new(probe) };
        var context = new ScriptContext() { CurrentHost = ConnectionPortResolverTests.Host("bad,list") };
        var shared = await service.RunAsync(context, new() { Session = InteractiveSessionMode.Shared }, default);
        shared.Success.Should().BeFalse("no shared session was provided");
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var action = () => service.RunAsync(context, new(), cts.Token);
        await action.Should().ThrowAsync<OperationCanceledException>();
        probe.Ports.Should().BeEmpty();
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task SeparateAndCaptureResolveBeforeResourceCreation(bool capture, bool alreadyResolved)
    {
        var host = ConnectionPortResolverTests.Host();
        host.Username = "test";
        host.Password = "test";
        var probe = new RecordingPortProbe();
        var resolver = new SSH_Helper.Services.ConnectionPortResolver(probe);
        var context = new ScriptContext(host.Variables)
        {
            CurrentHost = alreadyResolved ? await resolver.ResolveAsync(host, null, _ => { }, default) : host
        };
        probe.Ports.Clear();
        var service = new InteractiveTerminalService { PortResolver = resolver };
        var calls = 0;
        service.BeforeConnectionForTesting = effective =>
        {
            effective.Port.Should().Be(222);
            calls++;
            throw new InvalidOperationException("test terminal boundary");
        };
        var result = await service.RunAsync(context,
            new() { Session = InteractiveSessionMode.Separate, Command = capture ? "echo test" : null }, default);
        calls.Should().Be(1);
        result.Success.Should().BeFalse();
        probe.Ports.Should().HaveCount(alreadyResolved ? 0 : 2);
        context.CurrentHost!.Port.Should().Be(222);
        context.GetVariableString("port").Should().Be("222");
        host.Port.Should().Be(22);
    }
}
