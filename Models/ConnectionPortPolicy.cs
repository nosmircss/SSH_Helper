namespace SSH_Helper.Models;

public enum ConnectionPortSource { Default, SshConfig, Environment, Row, Endpoint, Programmatic, Preconnect }

public sealed record ConnectionPortPolicy(
    string RawPort, ConnectionPortSource Source, string? RawCheck, string? RawTimeout);
