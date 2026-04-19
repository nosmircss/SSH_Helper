using SSH_Helper.Models;

namespace SSH_Helper.Services.Notifications
{
    /// <summary>
    /// Orchestrates notify command dispatch. Resolves a profile + channel, fetches secrets from
    /// the injected providers (which typically wrap Windows Credential Manager), and delegates
    /// to the channel-specific dispatcher.
    /// </summary>
    public sealed class NotificationService : IDisposable
    {
        private readonly NotificationSettings _settings;
        private readonly Func<string, string?>? _webhookUrlProvider;
        private readonly Func<string, string?>? _smtpPasswordProvider;
        private readonly HttpClient _httpClient;
        private readonly bool _ownsHttpClient;
        private readonly WebhookDispatcher _webhookDispatcher;
        private readonly ToastDispatcher _toastDispatcher;
        private readonly SmtpDispatcher _smtpDispatcher;
        private bool _disposed;

        public NotificationService(
            NotificationSettings settings,
            Func<string, string?>? webhookUrlProvider = null,
            Func<string, string?>? smtpPasswordProvider = null,
            HttpMessageHandler? httpHandler = null,
            ToastDispatcher? toastDispatcher = null,
            SmtpDispatcher? smtpDispatcher = null)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _webhookUrlProvider = webhookUrlProvider;
            _smtpPasswordProvider = smtpPasswordProvider;

            if (httpHandler != null)
            {
                _httpClient = new HttpClient(httpHandler, disposeHandler: false)
                {
                    Timeout = TimeSpan.FromSeconds(30)
                };
                _ownsHttpClient = true;
            }
            else
            {
                _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
                _ownsHttpClient = true;
            }

            _webhookDispatcher = new WebhookDispatcher(_httpClient);
            _toastDispatcher = toastDispatcher ?? new ToastDispatcher();
            _smtpDispatcher = smtpDispatcher ?? new SmtpDispatcher();
        }

        public bool IsEnabled => _settings.Enabled;

        public string? ResolveDefaultProfileName(string? environmentOverride = null)
        {
            if (!string.IsNullOrWhiteSpace(environmentOverride))
                return environmentOverride;
            if (!string.IsNullOrWhiteSpace(_settings.DefaultProfileName))
                return _settings.DefaultProfileName;
            return null;
        }

        public NotificationProfile? GetProfile(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return null;
            foreach (var profile in _settings.Profiles)
            {
                if (string.Equals(profile.Name, name, StringComparison.OrdinalIgnoreCase))
                    return profile;
            }
            return null;
        }

        /// <summary>
        /// Sends a notification. Resolution rules:
        /// 1. If both <paramref name="channelOverride"/> and <paramref name="profileName"/> are set, channel must match profile.Kind.
        /// 2. If only profile is set, channel is inferred from profile.Kind.
        /// 3. If only channel is set, it must be <see cref="NotificationChannelKind.Toast"/>.
        /// 4. If neither, falls back to <see cref="NotificationSettings.DefaultProfileName"/>.
        /// </summary>
        public async Task<NotificationResult> SendAsync(
            string? profileName,
            string? channelOverride,
            string? title,
            string message,
            NotificationLevel level,
            IEnumerable<string>? mentions,
            CancellationToken cancellationToken)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(NotificationService));
            if (message == null) throw new ArgumentNullException(nameof(message));

            var mentionList = (mentions ?? Array.Empty<string>())
                .Where(m => !string.IsNullOrWhiteSpace(m))
                .Select(m => m.Trim())
                .ToList();

            NotificationProfile? profile = null;
            NotificationChannelKind kind;

            if (!string.IsNullOrWhiteSpace(profileName))
            {
                profile = GetProfile(profileName);
                if (profile == null)
                    return NotificationResult.Failure("", $"Notification profile '{profileName}' not found.");
            }

            if (!string.IsNullOrWhiteSpace(channelOverride))
            {
                if (!TryParseChannel(channelOverride, out var overrideKind))
                    return NotificationResult.Failure("", $"Unknown channel '{channelOverride}'. Expected: slack, teams, discord, toast, smtp.");

                if (profile != null && profile.Kind != overrideKind)
                    return NotificationResult.Failure("", $"Channel '{channelOverride}' does not match profile '{profile.Name}' kind '{profile.Kind}'.");

                kind = overrideKind;
            }
            else if (profile != null)
            {
                kind = profile.Kind;
            }
            else
            {
                var defaultName = ResolveDefaultProfileName();
                if (string.IsNullOrWhiteSpace(defaultName))
                    return NotificationResult.Failure("", "No profile or channel specified and no default profile is configured.");
                profile = GetProfile(defaultName);
                if (profile == null)
                    return NotificationResult.Failure("", $"Default notification profile '{defaultName}' not found.");
                kind = profile.Kind;
            }

            if (!_settings.Enabled && kind != NotificationChannelKind.Toast)
            {
                var channelName = kind.ToString().ToLowerInvariant();
                return NotificationResult.Failure(
                    channelName,
                    "Notifications are disabled in Settings. Enable Settings -> Notifications for Slack, Teams, Discord, or SMTP delivery.");
            }

            if (kind == NotificationChannelKind.Toast)
                return await _toastDispatcher.SendAsync(title, message, level, cancellationToken).ConfigureAwait(false);

            if (profile == null)
                return NotificationResult.Failure(kind.ToString().ToLowerInvariant(), $"Channel '{kind}' requires a profile.");

            switch (kind)
            {
                case NotificationChannelKind.Slack:
                case NotificationChannelKind.Teams:
                case NotificationChannelKind.Discord:
                {
                    var url = _webhookUrlProvider?.Invoke(profile.Name);
                    return await _webhookDispatcher.SendAsync(kind, url ?? "", title, message, level, mentionList, cancellationToken).ConfigureAwait(false);
                }
                case NotificationChannelKind.Smtp:
                {
                    var password = _smtpPasswordProvider?.Invoke(profile.Name);
                    return await _smtpDispatcher.SendAsync(profile, password, title, message, level, cancellationToken).ConfigureAwait(false);
                }
                default:
                    return NotificationResult.Failure(kind.ToString().ToLowerInvariant(), $"Unsupported channel kind: {kind}");
            }
        }

        private static bool TryParseChannel(string value, out NotificationChannelKind kind)
        {
            switch ((value ?? "").Trim().ToLowerInvariant())
            {
                case "slack": kind = NotificationChannelKind.Slack; return true;
                case "teams": kind = NotificationChannelKind.Teams; return true;
                case "discord": kind = NotificationChannelKind.Discord; return true;
                case "toast": kind = NotificationChannelKind.Toast; return true;
                case "smtp":
                case "email":
                case "mail":
                    kind = NotificationChannelKind.Smtp; return true;
                default:
                    kind = NotificationChannelKind.Slack;
                    return false;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_ownsHttpClient)
                _httpClient.Dispose();
        }
    }
}
