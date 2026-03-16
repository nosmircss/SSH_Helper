using System;
using System.Diagnostics;
using System.Globalization;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using SSH_Helper.Services.Scripting.Models;

namespace SSH_Helper.Services.Scripting.Commands
{
    /// <summary>
    /// Checks whether a TCP port is open, closed, or timed out.
    /// </summary>
    public class PortcheckCommand : IScriptCommand
    {
        public async Task<CommandResult> ExecuteAsync(ScriptStep step, ScriptContext context, CancellationToken cancellationToken)
        {
            if (step.Portcheck == null)
                return CommandResult.Fail("Portcheck command has no options");

            var options = step.Portcheck;
            var host = context.SubstituteVariables(options.Host ?? string.Empty).Trim();
            var port = options.Port > 0 ? options.Port : 22;
            var timeoutSeconds = options.Timeout > 0 ? options.Timeout : 5;
            var into = options.Into;

            if (string.IsNullOrWhiteSpace(host))
            {
                Capture(into, context, "closed", string.Empty);
                return ApplyOnError(step, "Portcheck requires 'host'");
            }

            using var client = new TcpClient();
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
            var stopwatch = Stopwatch.StartNew();

            try
            {
                await client.ConnectAsync(host, port, cts.Token);
                stopwatch.Stop();

                var latency = stopwatch.ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture);
                Capture(into, context, "open", latency);
                context.EmitOutput($"Portcheck: {host}:{port} open ({latency}ms)", ScriptOutputType.Debug);
                return CommandResult.Ok();
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                Capture(into, context, "timeout", string.Empty);
                return ApplyOnError(step, $"Portcheck timed out after {timeoutSeconds} seconds for {host}:{port}");
            }
            catch (SocketException ex)
            {
                stopwatch.Stop();
                var latency = stopwatch.ElapsedMilliseconds > 0
                    ? stopwatch.ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture)
                    : string.Empty;
                Capture(into, context, "closed", latency);
                return ApplyOnError(step, $"Portcheck closed for {host}:{port}: {ex.Message}");
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                var latency = stopwatch.ElapsedMilliseconds > 0
                    ? stopwatch.ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture)
                    : string.Empty;
                Capture(into, context, "closed", latency);
                return ApplyOnError(step, $"Portcheck error for {host}:{port}: {ex.Message}");
            }
        }

        private static void Capture(string? into, ScriptContext context, string status, string latency)
        {
            if (string.IsNullOrWhiteSpace(into))
                return;

            context.SetVariable(into, status);
            context.SetVariable(into + "_latency", latency);
        }

        private static CommandResult ApplyOnError(ScriptStep step, string message)
            => CommandResult.ApplyOnError(step, message);
    }
}
