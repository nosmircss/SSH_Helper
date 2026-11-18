using System.Globalization;
using SSH_Helper.Models;
using SSH_Helper.Utilities;

namespace SSH_Helper.Services;

internal sealed class ConnectionPortResolver(IConnectionPortProbe? probe = null)
{
    private readonly IConnectionPortProbe _probe = probe ?? new ConnectionPortProbe();

    internal async Task<HostConnection> ResolveAsync(HostConnection original, string? preconnectPort,
        Action<string> report, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (original.IsPortResolvedForRun && preconnectPort == null) return original;
        var host = original.CloneForRun();
        var policy = original.PortPolicy;
        int selected;
        if (preconnectPort != null)
        {
            if (preconnectPort.Contains(',') ||
                !InputValidator.TryParsePortCandidates(preconnectPort, out var ports, out _))
                throw new InvalidOperationException($"_ssh_port '{preconnectPort}' must be an integer between 1 and 65535");
            selected = ports[0];
            host.PortPolicy = new(preconnectPort, ConnectionPortSource.Preconnect, null, null);
        }
        else if (policy == null)
        {
            selected = original.Port;
        }
        else if (!policy.RawPort.Contains(','))
        {
            if (!InputValidator.TryParsePortCandidates(policy.RawPort, out var ports, out var error))
                throw new InvalidOperationException($"{policy.Source} port: {error}");
            selected = ports[0];
        }
        else
        {
            var (ports, mode, timeout) = ConnectionPortPolicyService.ValidateDiscovery(policy);
            var outcomes = new List<string>();
            int? winner = null;
            foreach (var port in ports)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var result = await _probe.CheckAsync(host, port, mode, timeout, cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                var outcome = $"{port}: {result.Status}" +
                    (string.IsNullOrEmpty(result.Diagnostic) ? "" : $" ({result.Diagnostic})");
                outcomes.Add(outcome);
                report($"{host.IpAddress}:{outcome}");
                if (result.Status == "open") { winner = port; break; }
            }
            selected = winner ?? throw new InvalidOperationException(
                $"No qualifying port for {host.IpAddress} ({policy.Source}): {string.Join("; ", outcomes)}");
        }
        host.Port = selected;
        host.Variables["port"] = selected.ToString(CultureInfo.InvariantCulture);
        host.IsPortResolvedForRun = true;
        report($"Selected SSH endpoint {host.IpAddress}:{selected}");
        return host;
    }
}
