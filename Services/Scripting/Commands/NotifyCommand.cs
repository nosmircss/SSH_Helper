using SSH_Helper.Models;
using SSH_Helper.Services.Notifications;
using SSH_Helper.Services.Scripting.Models;

namespace SSH_Helper.Services.Scripting.Commands
{
    /// <summary>
    /// Dispatches a notification via Slack/Teams/Discord webhook, Windows toast, or SMTP email.
    /// </summary>
    public class NotifyCommand : IScriptCommand
    {
        public async Task<CommandResult> ExecuteAsync(ScriptStep step, ScriptContext context, CancellationToken cancellationToken)
        {
            var options = step.Notify;
            if (options == null)
                return CommandResult.Fail("Notify command has no options");

            if (context.NotificationService == null)
                return ApplyOnError(step, "Notifications are not configured");

            var message = context.SubstituteVariables(options.Message ?? "");
            if (string.IsNullOrWhiteSpace(message))
                return ApplyOnError(step, "Notify requires 'message'");

            var profile = string.IsNullOrWhiteSpace(options.Profile) ? null : context.SubstituteVariables(options.Profile);
            var channel = string.IsNullOrWhiteSpace(options.Channel) ? null : context.SubstituteVariables(options.Channel);
            var title = string.IsNullOrWhiteSpace(options.Title) ? null : context.SubstituteVariables(options.Title);
            var levelRaw = context.SubstituteVariables(options.Level ?? "info");

            if (!TryParseLevel(levelRaw, out var level))
                return ApplyOnError(step, $"Invalid notify level '{levelRaw}'. Expected: info, warn, error, success.");

            var mentions = new List<string>();
            if (options.Mention != null)
            {
                foreach (var raw in options.Mention)
                {
                    var sub = context.SubstituteVariables(raw ?? "");
                    if (!string.IsNullOrWhiteSpace(sub))
                        mentions.Add(sub);
                }
            }

            if (ResolveEffectiveChannelKind(context.NotificationService, profile, channel) == NotificationChannelKind.Teams)
            {
                foreach (var warning in TeamsAdaptiveCardPayloadBuilder.CollectWarnings(mentions))
                    context.EmitOutput(warning, ScriptOutputType.Warning);
            }

            NotificationResult result;
            try
            {
                result = await context.NotificationService.SendAsync(
                    profile, channel, title, message, level, mentions, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return ApplyOnError(step, $"Notify dispatch failed: {ex.Message}");
            }

            CaptureResult(options.Into, result, context);

            if (result.Sent)
            {
                context.EmitOutput($"[notify] {result.Channel} -> sent", ScriptOutputType.Debug);
                return CommandResult.Ok();
            }

            var errorMsg = result.ErrorMessage ?? "notification failed";
            context.EmitOutput($"[notify] {result.Channel} -> {errorMsg}", ScriptOutputType.Debug);

            if (step.IsOnErrorContinue || string.Equals(options.OnError, "continue", StringComparison.OrdinalIgnoreCase))
            {
                context.SetVariable("_last_error", errorMsg);
                return CommandResult.Suppressed(errorMsg);
            }

            return CommandResult.Fail(errorMsg);
        }

        private static void CaptureResult(string? intoRaw, NotificationResult result, ScriptContext context)
        {
            var into = string.IsNullOrWhiteSpace(intoRaw) ? null : context.SubstituteVariables(intoRaw);
            if (string.IsNullOrWhiteSpace(into))
                return;

            context.SetVariable(into + ".sent", result.Sent ? "true" : "false");
            context.SetVariable(into + ".channel", result.Channel);
            if (result.StatusCode.HasValue)
                context.SetVariable(into + ".status_code", result.StatusCode.Value.ToString());
            if (!string.IsNullOrEmpty(result.ErrorMessage))
                context.SetVariable(into + ".error", result.ErrorMessage);
        }

        private static bool TryParseLevel(string value, out NotificationLevel level)
        {
            switch ((value ?? "").Trim().ToLowerInvariant())
            {
                case "":
                case "info":
                    level = NotificationLevel.Info; return true;
                case "warn":
                case "warning":
                    level = NotificationLevel.Warn; return true;
                case "error":
                case "err":
                    level = NotificationLevel.Error; return true;
                case "success":
                case "ok":
                    level = NotificationLevel.Success; return true;
                default:
                    level = NotificationLevel.Info;
                    return false;
            }
        }

        private static NotificationChannelKind? ResolveEffectiveChannelKind(NotificationService service, string? profileName, string? channelOverride)
        {
            if (!string.IsNullOrWhiteSpace(channelOverride))
            {
                switch (channelOverride.Trim().ToLowerInvariant())
                {
                    case "slack":
                        return NotificationChannelKind.Slack;
                    case "teams":
                        return NotificationChannelKind.Teams;
                    case "discord":
                        return NotificationChannelKind.Discord;
                    case "toast":
                        return NotificationChannelKind.Toast;
                    case "smtp":
                    case "email":
                    case "mail":
                        return NotificationChannelKind.Smtp;
                    default:
                        return null;
                }
            }

            if (!string.IsNullOrWhiteSpace(profileName))
                return service.GetProfile(profileName)?.Kind;

            var defaultProfileName = service.ResolveDefaultProfileName();
            if (!string.IsNullOrWhiteSpace(defaultProfileName))
                return service.GetProfile(defaultProfileName)?.Kind;

            return null;
        }

        private static CommandResult ApplyOnError(ScriptStep step, string message)
            => CommandResult.ApplyOnError(step, message);
    }
}
