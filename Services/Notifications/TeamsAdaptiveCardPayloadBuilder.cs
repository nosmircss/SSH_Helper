using Newtonsoft.Json.Linq;
using SSH_Helper.Models;

namespace SSH_Helper.Services.Notifications
{
    internal static class TeamsAdaptiveCardPayloadBuilder
    {
        internal static TeamsAdaptiveCardPayload Build(string? title, string message, NotificationLevel level, IReadOnlyList<string> mentions)
        {
            var parsedMentions = ParseMentions(mentions);
            var body = new JArray();

            if (!string.IsNullOrWhiteSpace(title))
            {
                body.Add(new JObject
                {
                    ["type"] = "TextBlock",
                    ["text"] = title,
                    ["weight"] = "Bolder",
                    ["size"] = "Medium",
                    ["color"] = MapTitleColor(level),
                    ["wrap"] = true
                });
            }

            if (!string.IsNullOrWhiteSpace(parsedMentions.VisibleText))
            {
                body.Add(new JObject
                {
                    ["type"] = "TextBlock",
                    ["text"] = parsedMentions.VisibleText,
                    ["wrap"] = true
                });
            }

            body.Add(new JObject
            {
                ["type"] = "TextBlock",
                ["text"] = message,
                ["wrap"] = true
            });

            var card = new JObject
            {
                ["$schema"] = "https://adaptivecards.io/schemas/adaptive-card.json",
                ["type"] = "AdaptiveCard",
                ["version"] = "1.2",
                ["body"] = body
            };

            if (parsedMentions.Entities.Count > 0)
            {
                card["msteams"] = new JObject
                {
                    ["entities"] = parsedMentions.Entities
                };
            }

            var payload = new JObject
            {
                ["type"] = "message",
                ["attachments"] = new JArray
                {
                    new JObject
                    {
                        ["contentType"] = "application/vnd.microsoft.card.adaptive",
                        ["contentUrl"] = null,
                        ["content"] = card
                    }
                }
            };

            return new TeamsAdaptiveCardPayload(payload, parsedMentions.Warnings);
        }

        internal static IReadOnlyList<string> CollectWarnings(IReadOnlyList<string> mentions)
            => ParseMentions(mentions).Warnings;

        private static ParsedMentions ParseMentions(IReadOnlyList<string> mentions)
        {
            var visibleParts = new List<string>();
            var entities = new JArray();
            var warnings = new List<string>();

            foreach (var mention in mentions)
            {
                var trimmed = (mention ?? string.Empty).Trim();
                if (trimmed.Length == 0)
                    continue;

                if (TryParseTypedMention(trimmed, "upn", IsValidUpn, out var upnId, out var upnDisplay))
                {
                    AddEntity(visibleParts, entities, upnId!, upnDisplay!);
                    continue;
                }

                if (TryParseTypedMention(trimmed, "entra", candidate => Guid.TryParse(candidate, out _), out var entraId, out var entraDisplay))
                {
                    AddEntity(visibleParts, entities, entraId!, entraDisplay!);
                    continue;
                }

                visibleParts.Add(trimmed);
                warnings.Add($"Notify: Teams mention '{trimmed}' is not a valid typed Teams mention; sending it as literal text.");
            }

            return new ParsedMentions(string.Join(" ", visibleParts), entities, warnings);
        }

        private static bool TryParseTypedMention(
            string input,
            string prefix,
            Func<string, bool> idValidator,
            out string? id,
            out string? display)
        {
            id = null;
            display = null;

            if (!input.StartsWith(prefix + ":", StringComparison.OrdinalIgnoreCase))
                return false;

            var remainder = input.Substring(prefix.Length + 1).Trim();
            if (remainder.Length == 0)
                return false;

            var separatorIndex = remainder.IndexOf('|');
            var rawId = separatorIndex >= 0 ? remainder.Substring(0, separatorIndex).Trim() : remainder;
            var rawDisplay = separatorIndex >= 0 ? remainder[(separatorIndex + 1)..].Trim() : string.Empty;

            if (!idValidator(rawId))
                return false;

            id = rawId;
            display = string.IsNullOrWhiteSpace(rawDisplay) ? rawId : rawDisplay;
            return true;
        }

        private static bool IsValidUpn(string candidate)
        {
            if (string.IsNullOrWhiteSpace(candidate))
                return false;

            return candidate.Contains('@', StringComparison.Ordinal)
                && !candidate.Any(char.IsWhiteSpace);
        }

        private static void AddEntity(List<string> visibleParts, JArray entities, string id, string display)
        {
            var tagText = $"<at>{display}</at>";
            visibleParts.Add(tagText);
            entities.Add(new JObject
            {
                ["type"] = "mention",
                ["text"] = tagText,
                ["mentioned"] = new JObject
                {
                    ["id"] = id,
                    ["name"] = display
                }
            });
        }

        private static string MapTitleColor(NotificationLevel level)
        {
            return level switch
            {
                NotificationLevel.Warn => "Warning",
                NotificationLevel.Error => "Attention",
                NotificationLevel.Success => "Good",
                _ => "Accent"
            };
        }

        internal sealed record TeamsAdaptiveCardPayload(JObject Payload, IReadOnlyList<string> Warnings);

        private sealed record ParsedMentions(string VisibleText, JArray Entities, IReadOnlyList<string> Warnings);
    }
}
