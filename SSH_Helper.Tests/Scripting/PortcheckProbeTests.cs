using System.Security.Cryptography;
using FluentAssertions;
using SSH_Helper.Models;
using SSH_Helper.Services.Scripting.Commands;
using Xunit;

namespace SSH_Helper.Tests.Scripting;

[CollectionDefinition("Portcheck timing", DisableParallelization = true)]
public class PortcheckTimingCollection;

[Collection("Portcheck timing")]
public class PortcheckProbeTests
{
    [Fact]
    public async Task SshHandshake_UnclassifiedConfigurationFailureIsNotAnUnreachableCandidate()
    {
        var action = () => PortcheckProbe.CheckAsync("host", 2222, "ssh", TimeSpan.FromSeconds(2),
            null, default, (_, _, _) => new HandshakeClient(() =>
                throw new Rebex.Net.SshException("Invalid client configuration")));
        await action.Should().ThrowAsync<Rebex.Net.SshException>().WithMessage("*configuration*");
    }

    [Fact]
    public async Task SshHandshake_ConfigurationFaultAfterDeadlineIsStillFatal()
    {
        var action = () => PortcheckProbe.CheckAsync("host", 2222, "ssh", TimeSpan.FromMilliseconds(50),
            null, default, (_, _, _) =>
            {
                Thread.Sleep(100);
                throw new InvalidOperationException("client configuration fault");
            });
        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("*configuration fault*");
    }

    [Fact]
    public async Task SshHandshake_FallbackTiersShareOneDeadline()
    {
        var timeouts = new List<int>();
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var result = await PortcheckProbe.CheckAsync("host", 2222, "ssh", TimeSpan.FromSeconds(5),
            null, default, (_, tier, timeout) =>
            {
                timeouts.Add(timeout);
                return new HandshakeClient(() =>
                {
                    if (tier == 0)
                    {
                        // Deliberately consume part of the one budget, not a tight duration assertion.
                        var started = clock.ElapsedMilliseconds;
                        SpinWait.SpinUntil(() => clock.ElapsedMilliseconds - started >= 80, TimeSpan.FromSeconds(2));
                        throw new CryptographicException("key algorithm is not supported");
                    }
                    if (tier == 1) throw new CryptographicException("key algorithm is not supported");
                });
            });
        result.Status.Should().Be("open");
        timeouts.Should().HaveCount(3);
        timeouts[1].Should().BeLessThan(timeouts[0]);
        timeouts[2].Should().BeLessThanOrEqualTo(timeouts[1]);
    }

    [Fact]
    public async Task SshHandshake_Success_ConnectsWithoutAuthenticationAndDisposesClient()
    {
        var client = new HandshakeClient(() => { });
        var result = await PortcheckProbe.CheckAsync("host", 2222, "ssh", TimeSpan.FromSeconds(1),
            null, CancellationToken.None, (_, _, _) => client);

        result.Status.Should().Be("open");
        client.Target.Should().Be(("host", 2222));
        client.Disposed.Should().BeTrue();
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public async Task SshHandshake_Interrupted_DisposesAndObservesWorker(bool cancel, bool sshException)
    {
        using var cts = new CancellationTokenSource();
        using var stopped = new ManualResetEventSlim();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new HandshakeClient(() =>
        {
            started.TrySetResult();
            if (!stopped.Wait(TimeSpan.FromSeconds(5)))
                throw new TimeoutException("Test handshake was not interrupted");
            if (sshException)
                throw new Rebex.Net.SshException("Ssh object was disposed while an operation was in progress.",
                    new TimeoutException("Connection attempt timed out."));
            throw new ObjectDisposedException("handshake");
        }, stopped.Set);
        var check = PortcheckProbe.CheckAsync("host", 2222, "ssh",
            TimeSpan.FromMilliseconds(cancel ? 5000 : 100), null, cts.Token, (_, _, _) => client);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        if (cancel)
        {
            cts.Cancel();
            await ((Func<Task>)(async () => await check)).Should().ThrowAsync<OperationCanceledException>();
        }
        else
        {
            var result = await check.WaitAsync(TimeSpan.FromSeconds(5));
            result.Status.Should().Be("timeout");
        }
        client.Disposed.Should().BeTrue();
        client.Completed.Should().BeTrue();
    }

    [Theory]
    [InlineData(false, 3)]
    [InlineData(true, 1)]
    public async Task SshHandshake_AlgorithmFallback_RespectsExplicitAlgorithms(bool explicitAlgorithms, int attempts)
    {
        var tiers = new List<int>();
        var clients = new List<HandshakeClient>();
        var host = new HostConnection { HostKeyAlgorithms = explicitAlgorithms ? new[] { "ssh-ed25519" } : null };
        var action = () => PortcheckProbe.CheckAsync("host", 2222, "ssh", TimeSpan.FromSeconds(1),
            host, CancellationToken.None, (_, tier, _) =>
            {
                tiers.Add(tier);
                var client = new HandshakeClient(() =>
                {
                    if (tier < 2)
                        throw new CryptographicException("key algorithm is not supported");
                });
                clients.Add(client);
                return client;
            });
        if (explicitAlgorithms)
            (await action()).Status.Should().Be("closed");
        else
            (await action()).Status.Should().Be("open");
        tiers.Should().Equal(Enumerable.Range(0, attempts));
        clients.Should().OnlyContain(client => client.Disposed);
    }

    private sealed class HandshakeClient(Action connect, Action? dispose = null) : PortcheckProbe.ISshHandshakeClient
    {
        public bool Disposed { get; private set; }
        public bool Completed { get; private set; }
        public (string, int) Target { get; private set; }
        public void Connect(string host, int port)
        {
            Target = (host, port);
            try { connect(); }
            finally { Completed = true; }
        }
        public void Dispose()
        {
            Disposed = true;
            dispose?.Invoke();
        }
    }
}
