using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using SSH_Helper.Services.Scripting.Models;
using SSH_Helper.Utilities;

namespace SSH_Helper.Services.Scripting.Commands
{
    /// <summary>
    /// Selects the first reachable TCP or SSH port from ordered candidates.
    /// </summary>
    public class PortcheckCommand : IScriptCommand
    {
        public async Task<CommandResult> ExecuteAsync(ScriptStep step, ScriptContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (step.Portcheck == null)
                return CommandResult.Fail("Portcheck command has no options");

            var options = step.Portcheck;
            Capture(options.Into, context, "closed", string.Empty, string.Empty);
            var error = ValidateOptions(options);
            if (error != null)
                return CommandResult.ApplyOnError(step, error);

            var host = context.SubstituteVariables(options.Host).Trim();
            if (string.IsNullOrWhiteSpace(host))
                return CommandResult.ApplyOnError(step, "Portcheck requires 'host'");

            var values = options.Ports ??
                (options.PortsFrom != null
                    ? ValueResolver.ResolveCollectionExpression(options.PortsFrom, context)
                    : new List<string> { options.Port.ToString(CultureInfo.InvariantCulture) });
            var candidates = new List<int>();
            foreach (var value in values)
            {
                foreach (var text in context.SubstituteVariables(value).Split(','))
                {
                    if (!int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var port) ||
                        !InputValidator.IsValidPort(port))
                        return CommandResult.ApplyOnError(step, $"Portcheck invalid port '{text}' (expected 1..65535)");
                    candidates.Add(port);
                }
            }
            if (candidates.Count == 0)
                return CommandResult.ApplyOnError(step, $"Portcheck ports source '{options.PortsFrom}' is missing or empty");

            var diagnostics = new List<string>();
            foreach (var port in candidates)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var result = await PortcheckProbe.CheckAsync(host, port, options.Mode,
                    TimeSpan.FromSeconds(options.Timeout), context.CurrentHost, cancellationToken);
                var selected = result.Status == "open" ? port.ToString(CultureInfo.InvariantCulture) : string.Empty;
                Capture(options.Into, context, result.Status, result.Latency, selected);
                if (result.Status == "open")
                {
                    context.EmitOutput($"Portcheck: {host}:{port} open ({result.Latency}ms, {options.Mode})", ScriptOutputType.Debug);
                    return CommandResult.Ok();
                }
                diagnostics.Add($"{port}: {result.Diagnostic}");
            }
            return CommandResult.ApplyOnError(step, $"Portcheck no matching {options.Mode} port for {host}: {string.Join("; ", diagnostics)}");
        }

        internal static string? ValidateOptions(PortcheckOptions options)
        {
            if (options.Mode is not ("tcp" or "ssh"))
                return "Portcheck mode must be tcp or ssh";
            if (options.Timeout <= 0 || options.Timeout > int.MaxValue / 1000)
                return "Portcheck timeout must be a positive number of seconds no greater than 2147483";
            if (options.HasExplicitPort && (options.Ports != null || options.PortsFrom != null))
                return "Portcheck cannot specify both port and ports";
            if (options.Ports is { Count: 0 } || options.PortsFrom != null && string.IsNullOrWhiteSpace(options.PortsFrom))
                return "Portcheck ports must not be empty";
            if (options.Ports == null && options.PortsFrom == null && !InputValidator.IsValidPort(options.Port))
                return "Portcheck port must be between 1 and 65535";
            return null;
        }

        private static void Capture(string? into, ScriptContext context, string status, string latency, string port)
        {
            if (string.IsNullOrWhiteSpace(into))
                return;
            context.SetVariable(into, status);
            context.SetVariable(into + "_latency", latency);
            context.SetVariable(into + "_port", port);
        }
    }
}
