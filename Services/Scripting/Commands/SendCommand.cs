using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using SSH_Helper.Services;
using SSH_Helper.Services.Scripting.Models;
using SSH_Helper.Utilities;

namespace SSH_Helper.Services.Scripting.Commands
{
    /// <summary>
    /// Executes an SSH command via the shell session.
    /// </summary>
    public class SendCommand : IScriptCommand
    {
        internal const string ExitStatusSentinel = "__SSH_HELPER_EXIT_STATUS_13B4A9E3__";

        private static readonly Regex ExitStatusRegex = new(
            $@"(?:\r?\n){Regex.Escape(ExitStatusSentinel)}:(?<status>\d+)\r?\n?$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private readonly Func<ScriptContext, ISendCommandSession?> _sessionResolver;

        public SendCommand()
            : this(CreateDefaultSession)
        {
        }

        internal SendCommand(Func<ScriptContext, ISendCommandSession?> sessionResolver)
        {
            _sessionResolver = sessionResolver ?? throw new ArgumentNullException(nameof(sessionResolver));
        }

        public async Task<CommandResult> ExecuteAsync(ScriptStep step, ScriptContext context, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(step.Send))
                return CommandResult.Fail("Send command has no command text");

            if (step.FailOnNonZero && (!string.IsNullOrWhiteSpace(step.Expect) || step.Respond is { Count: > 0 }))
            {
                return EmitFailure(
                    step,
                    context,
                    "send.fail_on_nonzero is only supported for prompt-waiting send steps without expect/respond");
            }

            var session = _sessionResolver(context);
            if (session == null)
            {
                return EmitFailure(step, context, "No SSH session available");
            }

            try
            {
                var command = context.SubstituteVariables(step.Send);
                var executedCommand = step.FailOnNonZero
                    ? BuildCommandWithExitStatusSentinel(command)
                    : command;

                if (!step.Suppress)
                {
                    var prompt = session.CurrentPrompt ?? ">>>";
                    context.EmitOutput($"{prompt} {command}", ScriptOutputType.Command);
                }

                var timeoutSeconds = step.Timeout.HasValue && step.Timeout.Value > 0 ? step.Timeout.Value : (int?)null;
                string output;

                if (step.Respond != null && step.Respond.Count > 0)
                {
                    var respondPairs = step.Respond
                        .Select(r => (
                            expectPattern: context.SubstituteVariables(r.Expect),
                            reply: context.SubstituteVariables(r.Reply)))
                        .ToList();

                    output = await session.ExecuteWithRespondsAsync(executedCommand, respondPairs, timeoutSeconds, cancellationToken);
                }
                else
                {
                    output = await session.ExecuteAsync(executedCommand, step.Expect, timeoutSeconds, cancellationToken);
                }

                output = TerminalOutputProcessor.StripCommandEcho(output, executedCommand);
                output = TerminalOutputProcessor.StripTrailingPrompt(output, session.CurrentPrompt);

                string? postExecutionFailure = null;
                if (step.FailOnNonZero)
                {
                    if (!TryExtractExitStatus(output, out output, out var exitStatus, out var exitStatusError))
                    {
                        postExecutionFailure = exitStatusError;
                    }
                    else if (exitStatus != 0)
                    {
                        postExecutionFailure = $"Command exited with status {exitStatus}";
                    }
                }

                context.RecordCommandOutput(output, step.Capture);

                if (!step.Suppress)
                {
                    context.EmitOutput(output, ScriptOutputType.CommandOutput);
                }

                if (!string.IsNullOrWhiteSpace(postExecutionFailure))
                {
                    return EmitFailure(step, context, postExecutionFailure);
                }

                return CommandResult.Ok();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return EmitFailure(step, context, $"Command failed: {ex.Message}");
            }
        }

        private static ISendCommandSession? CreateDefaultSession(ScriptContext context)
        {
            if (context.Session == null)
                return null;

            return new SshSendCommandSessionAdapter(context.Session);
        }

        private static string BuildCommandWithExitStatusSentinel(string command)
        {
            var escapedCommand = command.Replace("'", "'\"'\"'", StringComparison.Ordinal);
            return $"eval '{escapedCommand}'; __ssh_helper_send_status=$?; printf '\\n{ExitStatusSentinel}:%s\\n' \"$__ssh_helper_send_status\"";
        }

        private static bool TryExtractExitStatus(
            string output,
            out string cleanedOutput,
            out int exitStatus,
            out string errorMessage)
        {
            cleanedOutput = output;
            exitStatus = 0;
            errorMessage = string.Empty;

            var match = ExitStatusRegex.Match(output);
            if (match.Success && int.TryParse(match.Groups["status"].Value, out exitStatus))
            {
                cleanedOutput = output.Remove(match.Index);
                return true;
            }

            var sentinelIndex = output.LastIndexOf(ExitStatusSentinel, StringComparison.Ordinal);
            if (sentinelIndex >= 0)
            {
                cleanedOutput = StripSentinelTail(output, sentinelIndex);
                errorMessage = "Command exit status marker was malformed";
                return false;
            }

            errorMessage = "Command exit status marker was missing";
            return false;
        }

        private static string StripSentinelTail(string output, int sentinelIndex)
        {
            var removalIndex = sentinelIndex;
            if (removalIndex > 0 && output[removalIndex - 1] == '\n')
            {
                removalIndex--;
                if (removalIndex > 0 && output[removalIndex - 1] == '\r')
                {
                    removalIndex--;
                }
            }

            return output[..removalIndex];
        }

        private static CommandResult EmitFailure(ScriptStep step, ScriptContext context, string errorMsg)
        {
            context.EmitOutput(errorMsg, ScriptOutputType.Error);
            return ApplyOnError(step, errorMsg);
        }

        private static CommandResult ApplyOnError(ScriptStep step, string message)
            => CommandResult.ApplyOnError(step, message);

        internal interface ISendCommandSession
        {
            string? CurrentPrompt { get; }

            Task<string> ExecuteAsync(
                string command,
                string? expectPattern,
                int? timeoutSeconds,
                CancellationToken cancellationToken);

            Task<string> ExecuteWithRespondsAsync(
                string command,
                IReadOnlyList<(string expectPattern, string reply)> responds,
                int? timeoutSeconds,
                CancellationToken cancellationToken);
        }

        private sealed class SshSendCommandSessionAdapter : ISendCommandSession
        {
            private readonly SshShellSession _session;

            public SshSendCommandSessionAdapter(SshShellSession session)
            {
                _session = session ?? throw new ArgumentNullException(nameof(session));
            }

            public string? CurrentPrompt => _session.CurrentPrompt;

            public Task<string> ExecuteAsync(
                string command,
                string? expectPattern,
                int? timeoutSeconds,
                CancellationToken cancellationToken)
            {
                return _session.ExecuteAsync(command, expectPattern, timeoutSeconds, cancellationToken);
            }

            public Task<string> ExecuteWithRespondsAsync(
                string command,
                IReadOnlyList<(string expectPattern, string reply)> responds,
                int? timeoutSeconds,
                CancellationToken cancellationToken)
            {
                return _session.ExecuteWithRespondsAsync(command, responds, timeoutSeconds, cancellationToken);
            }
        }
    }
}
