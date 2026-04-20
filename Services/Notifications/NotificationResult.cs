namespace SSH_Helper.Services.Notifications
{
    /// <summary>
    /// Outcome of a single notify dispatch, returned to the script engine for capture via <c>into:</c>.
    /// </summary>
    public sealed class NotificationResult
    {
        /// <summary>True when the message was delivered to the underlying transport without error.</summary>
        public bool Sent { get; init; }

        /// <summary>Lower-cased channel name that actually handled the send (e.g. "slack", "toast", "smtp").</summary>
        public string Channel { get; init; } = "";

        /// <summary>HTTP status code for webhook channels; null for toast and SMTP.</summary>
        public int? StatusCode { get; init; }

        /// <summary>Error message when <see cref="Sent"/> is false; null on success.</summary>
        public string? ErrorMessage { get; init; }

        public static NotificationResult Success(string channel, int? statusCode = null)
            => new() { Sent = true, Channel = channel, StatusCode = statusCode };

        public static NotificationResult Failure(string channel, string errorMessage, int? statusCode = null)
            => new() { Sent = false, Channel = channel, StatusCode = statusCode, ErrorMessage = errorMessage };
    }
}
