using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SSH_Helper.Models;

namespace SSH_Helper.Services.Notifications
{
    /// <summary>
    /// Dispatches notifications to Slack/Teams/Discord incoming webhooks. All three share the same
    /// transport (POST JSON) and differ only in payload shape.
    /// </summary>
    internal sealed class WebhookDispatcher
    {
        private static readonly Regex WrappedSlackMentionRegex = new(@"^<[@!].+>$", RegexOptions.Compiled);
        private static readonly Regex SlackMemberIdRegex = new(@"^@?(?<id>[UW][A-Z0-9]{8,})$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex WrappedDiscordMentionRegex = new(@"^<(?:@!?\d+|@&\d+|#\d+)>$", RegexOptions.Compiled);
        private static readonly Regex DiscordTypedMentionRegex = new(
            @"^(?<kind>user|role|channel)\s*:\s*(?<id>\d+)$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private readonly HttpClient _httpClient;

        public WebhookDispatcher(HttpClient httpClient)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        }

        public async Task<NotificationResult> SendAsync(
            NotificationChannelKind kind,
            string webhookUrl,
            string? title,
            string message,
            NotificationLevel level,
            IReadOnlyList<string> mentions,
            CancellationToken cancellationToken)
        {
            var channelName = kind.ToString().ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(webhookUrl))
                return NotificationResult.Failure(channelName, "Webhook URL is not configured for this profile.");

            var payload = kind switch
            {
                NotificationChannelKind.Slack => BuildSlackPayload(title, message, level, mentions),
                NotificationChannelKind.Teams => TeamsAdaptiveCardPayloadBuilder.Build(title, message, level, mentions).Payload,
                NotificationChannelKind.Discord => BuildDiscordPayload(title, message, level, mentions),
                _ => throw new ArgumentException($"Webhook dispatcher does not support kind: {kind}", nameof(kind))
            };

            var json = JsonConvert.SerializeObject(payload);
            using var request = new HttpRequestMessage(HttpMethod.Post, webhookUrl)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            try
            {
                using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
                var status = (int)response.StatusCode;
                if (response.IsSuccessStatusCode)
                    return NotificationResult.Success(channelName, status);

                var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                var trimmed = string.IsNullOrWhiteSpace(body) ? response.ReasonPhrase ?? "" : body.Trim();
                return NotificationResult.Failure(channelName, $"HTTP {status}: {trimmed}", status);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return NotificationResult.Failure(channelName, ex.Message);
            }
        }

        private static JObject BuildSlackPayload(string? title, string message, NotificationLevel level, IReadOnlyList<string> mentions)
        {
            var color = level switch
            {
                NotificationLevel.Warn => "#FFC107",
                NotificationLevel.Error => "#F44336",
                NotificationLevel.Success => "#4CAF50",
                _ => "#2196F3"
            };

            var normalizedMentions = mentions
                .Select(NormalizeSlackMention)
                .Where(m => !string.IsNullOrWhiteSpace(m))
                .ToList();
            var mentionPrefix = normalizedMentions.Count > 0 ? string.Join(" ", normalizedMentions) + " " : "";
            var attachment = new JObject
            {
                ["color"] = color,
                ["text"] = message
            };
            if (!string.IsNullOrWhiteSpace(title))
                attachment["title"] = title;

            return new JObject
            {
                ["text"] = mentionPrefix + (title ?? ""),
                ["attachments"] = new JArray { attachment }
            };
        }

        private static string NormalizeSlackMention(string mention)
        {
            var trimmed = (mention ?? string.Empty).Trim();
            if (trimmed.Length == 0)
                return string.Empty;

            if (WrappedSlackMentionRegex.IsMatch(trimmed))
                return trimmed;

            var special = trimmed.TrimStart('@');
            if (special.Equals("here", StringComparison.OrdinalIgnoreCase))
                return "<!here>";
            if (special.Equals("channel", StringComparison.OrdinalIgnoreCase))
                return "<!channel>";
            if (special.Equals("everyone", StringComparison.OrdinalIgnoreCase))
                return "<!everyone>";

            var memberIdMatch = SlackMemberIdRegex.Match(trimmed);
            if (memberIdMatch.Success)
                return $"<@{memberIdMatch.Groups["id"].Value.ToUpperInvariant()}>";

            return trimmed;
        }

        private static JObject BuildDiscordPayload(string? title, string message, NotificationLevel level, IReadOnlyList<string> mentions)
        {
            var color = level switch
            {
                NotificationLevel.Warn => 16776960,
                NotificationLevel.Error => 15158332,
                NotificationLevel.Success => 3066993,
                _ => 3447003
            };

            var embed = new JObject
            {
                ["description"] = message,
                ["color"] = color
            };
            if (!string.IsNullOrWhiteSpace(title))
                embed["title"] = title;

            var payload = new JObject
            {
                ["embeds"] = new JArray { embed }
            };
            var normalizedMentions = mentions
                .Select(NormalizeDiscordMention)
                .Where(m => !string.IsNullOrWhiteSpace(m))
                .ToList();
            if (normalizedMentions.Count > 0)
                payload["content"] = string.Join(" ", normalizedMentions);

            return payload;
        }

        private static string NormalizeDiscordMention(string mention)
        {
            var trimmed = (mention ?? string.Empty).Trim();
            if (trimmed.Length == 0)
                return string.Empty;

            if (WrappedDiscordMentionRegex.IsMatch(trimmed))
                return trimmed;

            var special = trimmed.TrimStart('@');
            if (special.Equals("here", StringComparison.OrdinalIgnoreCase))
                return "@here";
            if (special.Equals("everyone", StringComparison.OrdinalIgnoreCase))
                return "@everyone";

            var typedMatch = DiscordTypedMentionRegex.Match(trimmed);
            if (!typedMatch.Success)
                return trimmed;

            var id = typedMatch.Groups["id"].Value;
            return typedMatch.Groups["kind"].Value.ToLowerInvariant() switch
            {
                "user" => $"<@{id}>",
                "role" => $"<@&{id}>",
                "channel" => $"<#{id}>",
                _ => trimmed
            };
        }
    }
}
