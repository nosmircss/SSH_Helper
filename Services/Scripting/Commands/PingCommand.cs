using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using SSH_Helper.Services.Scripting.Models;

namespace SSH_Helper.Services.Scripting.Commands
{
    /// <summary>
    /// Executes ICMP ping checks and captures normalized status/metrics.
    /// </summary>
    public class PingCommand : IScriptCommand
    {
        private readonly IPingProbe _probe;

        public PingCommand()
            : this(new SystemPingProbe())
        {
        }

        internal PingCommand(IPingProbe probe)
        {
            _probe = probe ?? throw new ArgumentNullException(nameof(probe));
        }

        public async Task<CommandResult> ExecuteAsync(ScriptStep step, ScriptContext context, CancellationToken cancellationToken)
        {
            if (step.Ping == null)
                return CommandResult.Fail("Ping command has no options");

            var options = step.Ping;
            var host = context.SubstituteVariables(options.Host ?? string.Empty).Trim();
            var into = options.Into;

            if (string.IsNullOrWhiteSpace(host))
            {
                CaptureFailure(into, context);
                return ApplyOnError(step, "Ping requires 'host'");
            }

            var count = options.Count > 0 ? options.Count : 4;
            var timeoutMs = options.Timeout > 0 ? options.Timeout : 3000;

            var successCount = 0;
            var failureCount = 0;
            var latencies = new List<long>();

            try
            {
                for (int i = 0; i < count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        var reply = await _probe.SendAsync(host, timeoutMs);
                        if (reply.Status == IPStatus.Success)
                        {
                            successCount++;
                            latencies.Add(reply.RoundtripTime);
                        }
                        else
                        {
                            failureCount++;
                        }
                    }
                    catch (PingException)
                    {
                        failureCount++;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                CaptureFailure(into, context);
                return ApplyOnError(step, $"Ping error: {ex.Message}");
            }

            if (successCount == 0)
            {
                CaptureFailure(into, context);
                return ApplyOnError(step, $"Ping failed: no replies from {host}");
            }

            var avgLatency = latencies.Count > 0 ? Math.Round(latencies.Average()) : 0d;
            var lossPercent = (int)Math.Round((double)failureCount * 100 / count, MidpointRounding.AwayFromZero);
            CaptureSuccess(into, context, avgLatency, lossPercent);

            context.EmitOutput($"Ping: {host} success={successCount}/{count}, avg={avgLatency}ms, loss={lossPercent}%", ScriptOutputType.Debug);
            return CommandResult.Ok();
        }

        private static void CaptureSuccess(string? into, ScriptContext context, double avgLatency, int lossPercent)
        {
            if (string.IsNullOrWhiteSpace(into))
                return;

            context.SetVariable(into, "success");
            context.SetVariable(into + "_avg", avgLatency.ToString(CultureInfo.InvariantCulture));
            context.SetVariable(into + "_loss", lossPercent.ToString(CultureInfo.InvariantCulture));
        }

        private static void CaptureFailure(string? into, ScriptContext context)
        {
            if (string.IsNullOrWhiteSpace(into))
                return;

            context.SetVariable(into, "failure");
            context.SetVariable(into + "_avg", string.Empty);
            context.SetVariable(into + "_loss", "100");
        }

        private static CommandResult ApplyOnError(ScriptStep step, string message)
            => CommandResult.ApplyOnError(step, message);

        internal interface IPingProbe
        {
            Task<PingProbeResult> SendAsync(string host, int timeoutMs);
        }

        internal readonly record struct PingProbeResult(IPStatus Status, long RoundtripTime);

        private sealed class SystemPingProbe : IPingProbe
        {
            public async Task<PingProbeResult> SendAsync(string host, int timeoutMs)
            {
                using var ping = new Ping();
                var reply = await ping.SendPingAsync(host, timeoutMs);
                return new PingProbeResult(reply.Status, reply.RoundtripTime);
            }
        }
    }
}
