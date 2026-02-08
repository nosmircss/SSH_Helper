using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using SSH_Helper.Services.Scripting.Models;

namespace SSH_Helper.Services.Scripting.Commands
{
    /// <summary>
    /// Resolves DNS records and captures results as a list variable.
    /// </summary>
    public class DnsCommand : IScriptCommand
    {
        private readonly IDnsResolver _resolver;

        public DnsCommand()
            : this(new SystemDnsResolver())
        {
        }

        internal DnsCommand(IDnsResolver resolver)
        {
            _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        }

        public async Task<CommandResult> ExecuteAsync(ScriptStep step, ScriptContext context, CancellationToken cancellationToken)
        {
            if (step.Dns == null)
                return CommandResult.Fail("Dns command has no options");

            var options = step.Dns;
            var host = context.SubstituteVariables(options.Host ?? string.Empty).Trim();
            var recordType = context.SubstituteVariables(options.Type ?? "A").Trim().ToUpperInvariant();
            var into = options.Into;

            if (string.IsNullOrWhiteSpace(host))
            {
                Capture(into, context, new List<string>());
                return ApplyOnError(step, "Dns requires 'host'");
            }

            if (!IsSupportedType(recordType))
            {
                Capture(into, context, new List<string>());
                return ApplyOnError(step, $"Dns type '{recordType}' is not supported");
            }

            var timeoutSeconds = options.Timeout > 0 ? options.Timeout : 10;
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

            try
            {
                List<string> values;
                switch (recordType)
                {
                    case "A":
                    {
                        var addresses = await _resolver.GetHostAddressesAsync(host, cts.Token);
                        values = addresses
                            .Where(a => a.AddressFamily == AddressFamily.InterNetwork)
                            .Select(a => a.ToString())
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .ToList();
                        break;
                    }
                    case "AAAA":
                    {
                        var addresses = await _resolver.GetHostAddressesAsync(host, cts.Token);
                        values = addresses
                            .Where(a => a.AddressFamily == AddressFamily.InterNetworkV6)
                            .Select(a => a.ToString())
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .ToList();
                        break;
                    }
                    case "PTR":
                    {
                        var entry = await _resolver.GetHostEntryAsync(host, cts.Token);
                        values = new List<string>();
                        if (!string.IsNullOrWhiteSpace(entry.HostName))
                            values.Add(entry.HostName);
                        values.AddRange(entry.Aliases.Where(a => !string.IsNullOrWhiteSpace(a)));
                        values = values.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                        break;
                    }
                    default:
                        values = new List<string>();
                        break;
                }

                Capture(into, context, values);
                context.EmitOutput($"Dns: {recordType} {host} -> {values.Count} record(s)", ScriptOutputType.Debug);
                return CommandResult.Ok();
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                Capture(into, context, new List<string>());
                return ApplyOnError(step, $"Dns lookup timed out after {timeoutSeconds} seconds");
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.HostNotFound || ex.SocketErrorCode == SocketError.NoData)
            {
                // No-record response: treat as success with empty list and count 0.
                Capture(into, context, new List<string>());
                context.EmitOutput($"Dns: {recordType} {host} returned no records", ScriptOutputType.Debug);
                return CommandResult.Ok();
            }
            catch (Exception ex)
            {
                Capture(into, context, new List<string>());
                return ApplyOnError(step, $"Dns error: {ex.Message}");
            }
        }

        private static bool IsSupportedType(string type)
        {
            return string.Equals(type, "A", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(type, "AAAA", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(type, "PTR", StringComparison.OrdinalIgnoreCase);
        }

        private static void Capture(string? into, ScriptContext context, List<string> values)
        {
            if (string.IsNullOrWhiteSpace(into))
                return;

            context.SetVariable(into, values);
            context.SetVariable(into + "_count", values.Count);
        }

        private static CommandResult ApplyOnError(ScriptStep step, string message)
        {
            if (string.Equals(step.OnError, "continue", StringComparison.OrdinalIgnoreCase))
                return CommandResult.Suppressed(message);

            return CommandResult.Fail(message);
        }

        internal interface IDnsResolver
        {
            Task<IPAddress[]> GetHostAddressesAsync(string host, CancellationToken cancellationToken);
            Task<IPHostEntry> GetHostEntryAsync(string host, CancellationToken cancellationToken);
        }

        private sealed class SystemDnsResolver : IDnsResolver
        {
            public Task<IPAddress[]> GetHostAddressesAsync(string host, CancellationToken cancellationToken)
            {
                return Dns.GetHostAddressesAsync(host, cancellationToken);
            }

            public Task<IPHostEntry> GetHostEntryAsync(string host, CancellationToken cancellationToken)
            {
                return Dns.GetHostEntryAsync(host, cancellationToken);
            }
        }
    }
}
