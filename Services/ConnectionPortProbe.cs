using System;
using System.Diagnostics;
using System.Globalization;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Rebex.Net;
using SSH_Helper.Models;

namespace SSH_Helper.Services
{
    internal sealed record ConnectionPortProbeResult(string Status, string Latency, string Diagnostic);

    internal interface IConnectionPortProbe
    {
        Task<ConnectionPortProbeResult> CheckAsync(HostConnection host, int port, string mode,
            TimeSpan timeout, CancellationToken cancellationToken);
    }

    internal sealed class ConnectionPortProbe : IConnectionPortProbe
    {
        public Task<ConnectionPortProbeResult> CheckAsync(HostConnection host, int port, string mode,
            TimeSpan timeout, CancellationToken cancellationToken) =>
            CheckAsync(host.IpAddress, port, mode, timeout, host, cancellationToken);

        internal interface ISshHandshakeClient : IDisposable
        {
            void Connect(string host, int port);
        }

        internal static async Task<ConnectionPortProbeResult> CheckAsync(string host, int port, string mode, TimeSpan timeout,
            HostConnection? currentHost, CancellationToken cancellationToken,
            Func<HostConnection, int, int, ISshHandshakeClient>? createSshClient = null)
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(timeout);
            var clock = Stopwatch.StartNew();
            try
            {
                if (mode == "tcp")
                {
                    using var client = new TcpClient();
                    await client.ConnectAsync(host, port, deadline.Token);
                }
                else
                {
                    await Task.Run(() => CheckSsh(host, port, timeout, clock, currentHost, deadline.Token,
                        createSshClient ?? CreateSshClient), deadline.Token);
                }
                deadline.Token.ThrowIfCancellationRequested();
                return new ConnectionPortProbeResult("open", clock.ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture), string.Empty);
            }
            catch (Exception) when (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException(cancellationToken);
            }
            catch (Exception ex) when (deadline.IsCancellationRequested &&
                (ex is OperationCanceledException or ObjectDisposedException || IsTransportFailure(ex)))
            {
                return new ConnectionPortProbeResult("timeout", string.Empty, $"timed out after {timeout.TotalSeconds}s");
            }
            catch (SshException ex) when (ex.Status == SshExceptionStatus.Timeout)
            {
                return new ConnectionPortProbeResult("timeout", string.Empty, ex.Message);
            }
            catch (Exception ex) when (IsTransportFailure(ex))
            {
                return new ConnectionPortProbeResult("closed", clock.ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture), ex.Message);
            }
        }

        private static bool IsTransportFailure(Exception ex) =>
            ex is SocketException or CryptographicException ||
            ex is SshException { Status: SshExceptionStatus.ConnectFailure or SshExceptionStatus.ConnectionClosed
                or SshExceptionStatus.ProtocolError or SshExceptionStatus.UnexpectedMessage
                or SshExceptionStatus.SocketError or SshExceptionStatus.Timeout };

        private static void CheckSsh(string host, int port, TimeSpan timeout, Stopwatch clock,
            HostConnection? currentHost, CancellationToken cancellationToken,
            Func<HostConnection, int, int, ISshHandshakeClient> createClient)
        {
            var target = new HostConnection
            {
                IpAddress = host,
                Port = port,
                HostKeyAlgorithms = currentHost?.HostKeyAlgorithms,
                Ciphers = currentHost?.Ciphers
            };
            for (var tier = 0; tier < 3; tier++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var ssh = createClient(target, tier, Math.Max(1, (int)(timeout - clock.Elapsed).TotalMilliseconds));
                // Disposing closes the transport so cancellation also interrupts a blocked handshake.
                using var registration = cancellationToken.Register(ssh.Dispose);
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    ssh.Connect(host, port);
                    cancellationToken.ThrowIfCancellationRequested();
                    return;
                }
                catch (SshException ex) when (cancellationToken.IsCancellationRequested)
                {
                    // Rebex can report disposal during Connect as SshException rather than ObjectDisposedException.
                    throw new OperationCanceledException("SSH handshake interrupted.", ex, cancellationToken);
                }
                catch (Exception ex) when (tier < 2 && !cancellationToken.IsCancellationRequested &&
                    SshExecutionService.ShouldRetryWithAlgorithmFallback(target, ex))
                {
                    // Retry known algorithm compatibility tiers within the original candidate deadline.
                }
            }
        }

        private static ISshHandshakeClient CreateSshClient(HostConnection target, int tier, int timeout)
        {
            var ssh = new Ssh { Timeout = timeout };
            try
            {
                SshExecutionService.ApplyAlgorithmSettings(ssh, target);
                if (tier > 0)
                    ssh.Settings.SshParameters.SetHostKeyAlgorithms(tier == 1
                        ? SshExecutionService.NonRsaHostKeyFallbackAlgorithms
                        : SshExecutionService.Ed25519OnlyHostKeyAlgorithms);
                if (tier == 2)
                {
                    ssh.Settings.SshParameters.SetEncryptionAlgorithms(SshExecutionService.ConservativeEncryptionFallbackAlgorithms);
                    SshExecutionService.TrySetSshParameterAlgorithms(ssh.Settings.SshParameters, "SetMacAlgorithms",
                        SshExecutionService.ConservativeMacFallbackAlgorithms);
                }
                return new RebexHandshakeClient(ssh);
            }
            catch
            {
                ssh.Dispose();
                throw;
            }
        }

        private sealed class RebexHandshakeClient(Ssh client) : ISshHandshakeClient
        {
            public void Connect(string host, int port) => client.Connect(host, port);
            public void Dispose() => client.Dispose();
        }
    }
}
