using System.Globalization;
using SSH_Helper.Models;
using SSH_Helper.Utilities;

namespace SSH_Helper.Services;

internal static class ConnectionPortPolicyService
{
    internal const string HelpText =
        "Port: a fixed number or ordered list such as 22,222,2222. Blank inherits environment; Host_IP :port overrides. " +
        "port_check: ssh (default, no authentication) or tcp. port_check_timeout: seconds per candidate; default 2.";

    // Credential Manager historically used only the written endpoint/row scalar and row/toolbar
    // username, before SSH-config fallback. Never key stored credentials by a discovered winner.
    internal static (string HostKey, string Username) CaptureLegacyCredentialIdentity(
        string address, IReadOnlyDictionary<string, string> row, string defaultUsername)
    {
        string? Read(string key) => row.FirstOrDefault(pair =>
            string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase)).Value;
        var host = HostConnection.Parse(address);
        var rowPort = Read("port");
        if (!host.HasExplicitPort && rowPort != null && !rowPort.Contains(',') &&
            InputValidator.TryParsePortCandidates(rowPort, out var ports, out _))
            host.Port = ports[0];
        var username = Read("username");
        return (host.ToString(), string.IsNullOrWhiteSpace(username) ? defaultUsername : username);
    }

    internal static string? GetAdvisory(HostConnection host)
    {
        if (host.PortPolicy is not { } policy) return null;
        try
        {
            if (policy.RawPort.Contains(','))
                ValidateDiscovery(policy);
            else if (!InputValidator.TryParsePortCandidates(policy.RawPort, out _, out var error))
                throw new InvalidOperationException($"{policy.Source} port: {error}");
            return null;
        }
        catch (InvalidOperationException ex)
        {
            return $"Advisory: {ex.Message}. A preconnect _ssh_port may override this policy.";
        }
    }

    internal static void Apply(HostConnection host, IReadOnlyDictionary<string, string> row,
        IReadOnlyDictionary<string, string> environment, SshHostConfig? sshConfig)
    {
        static string? Read(IReadOnlyDictionary<string, string> values, string name) =>
            values.FirstOrDefault(v => string.Equals(v.Key, name, StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(v.Value)).Value;
        string? Selected(string name) => Read(row, name) ?? Read(environment, name);

        var rowPort = Read(row, "port");
        var envPort = Read(environment, "port");
        var explicitPort = host.HasExplicitPort;
        var raw = explicitPort ? host.Port.ToString(CultureInfo.InvariantCulture)
            : rowPort ?? envPort ?? (sshConfig?.Port ?? 22).ToString(CultureInfo.InvariantCulture);
        var source = explicitPort ? (host.HasEndpointPort ? ConnectionPortSource.Endpoint : ConnectionPortSource.Programmatic)
            : rowPort != null ? ConnectionPortSource.Row
            : envPort != null ? ConnectionPortSource.Environment
            : sshConfig?.Port != null ? ConnectionPortSource.SshConfig : ConnectionPortSource.Default;

        host.PortPolicy = new(raw.Trim(), source, Selected("port_check"), Selected("port_check_timeout"));
        // A pending policy suppresses config port fallback but retains auth/algorithms.
        host.ApplySshConfig(sshConfig);
        var variables = new Dictionary<string, string>(host.Variables, StringComparer.OrdinalIgnoreCase);
        foreach (var pair in environment) variables[pair.Key] = pair.Value;
        foreach (var pair in row)
            if (!string.IsNullOrWhiteSpace(pair.Value) || !variables.ContainsKey(pair.Key))
                variables[pair.Key] = pair.Value;
        variables["port"] = raw.Trim();
        host.Variables = variables;
        if (!raw.Contains(',') && InputValidator.TryParsePortCandidates(raw, out var ports, out _))
            host.Port = ports[0];
    }

    internal static (IReadOnlyList<int> Ports, string Mode, TimeSpan Timeout) ValidateDiscovery(ConnectionPortPolicy policy)
    {
        if (!InputValidator.TryParsePortCandidates(policy.RawPort, out var ports, out var error))
            throw new InvalidOperationException($"{policy.Source} port: {error}");
        var mode = string.IsNullOrWhiteSpace(policy.RawCheck) ? "ssh" : policy.RawCheck.Trim().ToLowerInvariant();
        if (mode is not ("ssh" or "tcp"))
            throw new InvalidOperationException($"port_check '{policy.RawCheck}' must be ssh or tcp ({policy.Source} port)");
        var seconds = 2;
        if (!string.IsNullOrWhiteSpace(policy.RawTimeout) &&
            !InputValidator.TryParsePortCheckTimeout(policy.RawTimeout, out seconds))
            throw new InvalidOperationException($"port_check_timeout '{policy.RawTimeout}' must be integer seconds from 1 to 2147483 ({policy.Source} port)");
        return (ports, mode, TimeSpan.FromSeconds(seconds));
    }
}
