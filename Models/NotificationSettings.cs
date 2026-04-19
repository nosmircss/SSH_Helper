namespace SSH_Helper.Models
{
    /// <summary>
    /// Root configuration for the notify scripting command.
    /// </summary>
    public class NotificationSettings
    {
        /// <summary>
        /// When true, the notify command can dispatch through the configured profiles.
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// Named notification profiles (Slack/Teams/Discord webhooks, SMTP email).
        /// The Windows toast channel does not require a profile.
        /// </summary>
        public List<NotificationProfile> Profiles { get; set; } = new();

        /// <summary>
        /// The profile name used when no explicit profile or channel is specified on a notify step.
        /// </summary>
        public string DefaultProfileName { get; set; } = "";
    }

    /// <summary>
    /// Configuration for a single named notification profile.
    /// Secrets (webhook URL, SMTP password) are stored in Windows Credential Manager, not in this model.
    /// </summary>
    public class NotificationProfile
    {
        /// <summary>
        /// Unique name for this profile (e.g. "ops-alerts", "nightly-report").
        /// </summary>
        public string Name { get; set; } = "";

        /// <summary>
        /// Channel kind determines both the transport and the payload shape.
        /// </summary>
        public NotificationChannelKind Kind { get; set; } = NotificationChannelKind.Slack;

        /// <summary>
        /// Optional default title used when a notify step does not provide one.
        /// </summary>
        public string DefaultTitle { get; set; } = "";

        // --- SMTP-only fields (ignored for webhook channels) ---

        /// <summary>
        /// SMTP server host (e.g. "smtp.example.com"). SMTP channel only.
        /// </summary>
        public string SmtpHost { get; set; } = "";

        /// <summary>
        /// SMTP server port. Default: 587 (STARTTLS).
        /// </summary>
        public int SmtpPort { get; set; } = 587;

        /// <summary>
        /// Sender address used in the From header.
        /// </summary>
        public string SmtpFromAddress { get; set; } = "";

        /// <summary>
        /// One or more recipient addresses. Semicolons or commas are accepted in the UI but stored split.
        /// </summary>
        public List<string> SmtpToAddresses { get; set; } = new();

        /// <summary>
        /// SMTP auth username (optional if the relay accepts unauthenticated submission).
        /// The password is stored in Windows Credential Manager.
        /// </summary>
        public string SmtpUsername { get; set; } = "";

        /// <summary>
        /// When true, upgrades the SMTP connection to TLS via STARTTLS. Default: true.
        /// </summary>
        public bool UseStartTls { get; set; } = true;
    }

    /// <summary>
    /// Transport/channel kind for a notification profile.
    /// </summary>
    public enum NotificationChannelKind
    {
        /// <summary>Slack incoming webhook. URL stored in Credential Manager.</summary>
        Slack = 0,

        /// <summary>Microsoft Teams incoming webhook. URL stored in Credential Manager.</summary>
        Teams = 1,

        /// <summary>Discord webhook. URL stored in Credential Manager.</summary>
        Discord = 2,

        /// <summary>Windows desktop toast notification. No profile required.</summary>
        Toast = 3,

        /// <summary>SMTP email. Host/port/from/to stored in profile; password in Credential Manager.</summary>
        Smtp = 4
    }

    /// <summary>
    /// Level of a notification — drives channel-native color/icon/subject-prefix.
    /// </summary>
    public enum NotificationLevel
    {
        Info = 0,
        Warn = 1,
        Error = 2,
        Success = 3
    }
}
