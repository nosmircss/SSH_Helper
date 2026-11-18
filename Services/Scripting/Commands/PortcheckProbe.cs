using SSH_Helper.Models;

namespace SSH_Helper.Services.Scripting.Commands;

// Compatibility adapter for YAML portcheck; the transport/deadline is shared with automatic discovery.
internal static class PortcheckProbe
{
    internal interface ISshHandshakeClient : ConnectionPortProbe.ISshHandshakeClient { }

    internal static Task<ConnectionPortProbeResult> CheckAsync(string host, int port, string mode,
        TimeSpan timeout, HostConnection? currentHost, CancellationToken cancellationToken,
        Func<HostConnection, int, int, ISshHandshakeClient>? createSshClient = null) =>
        ConnectionPortProbe.CheckAsync(host, port, mode, timeout, currentHost, cancellationToken,
            createSshClient == null ? null : (target, tier, milliseconds) => createSshClient(target, tier, milliseconds));
}
