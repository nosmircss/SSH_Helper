using System.Net;
using System.Net.Mail;
using SSH_Helper.Models;

namespace SSH_Helper.Services.Notifications
{
    /// <summary>
    /// Sends notifications as SMTP email via <see cref="SmtpClient"/>. Password is supplied by the
    /// caller (resolved from Credential Manager by <see cref="NotificationService"/>).
    /// </summary>
    public class SmtpDispatcher
    {
        public virtual async Task<NotificationResult> SendAsync(
            NotificationProfile profile,
            string? password,
            string? title,
            string message,
            NotificationLevel level,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(profile.SmtpHost))
                return NotificationResult.Failure("smtp", "SMTP host is not configured on the profile.");
            if (string.IsNullOrWhiteSpace(profile.SmtpFromAddress))
                return NotificationResult.Failure("smtp", "SMTP from address is not configured on the profile.");
            if (profile.SmtpToAddresses.Count == 0)
                return NotificationResult.Failure("smtp", "SMTP to addresses are not configured on the profile.");

            var prefix = level switch
            {
                NotificationLevel.Warn => "[WARN]",
                NotificationLevel.Error => "[ERROR]",
                NotificationLevel.Success => "[OK]",
                _ => "[INFO]"
            };
            var subject = $"{prefix} {(string.IsNullOrWhiteSpace(title) ? "Notification" : title)}";

            using var mail = new MailMessage
            {
                From = new MailAddress(profile.SmtpFromAddress),
                Subject = subject,
                Body = message,
                IsBodyHtml = false
            };
            foreach (var to in profile.SmtpToAddresses)
            {
                var trimmed = (to ?? string.Empty).Trim();
                if (trimmed.Length > 0)
                    mail.To.Add(trimmed);
            }
            if (mail.To.Count == 0)
                return NotificationResult.Failure("smtp", "SMTP to addresses list is empty after trimming.");

            using var client = new SmtpClient(profile.SmtpHost, profile.SmtpPort)
            {
                EnableSsl = profile.UseStartTls,
                DeliveryMethod = SmtpDeliveryMethod.Network
            };
            if (!string.IsNullOrWhiteSpace(profile.SmtpUsername))
            {
                client.UseDefaultCredentials = false;
                client.Credentials = new NetworkCredential(profile.SmtpUsername, password ?? "");
            }

            try
            {
                await client.SendMailAsync(mail, cancellationToken).ConfigureAwait(false);
                return NotificationResult.Success("smtp");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return NotificationResult.Failure("smtp", ex.Message);
            }
        }
    }
}
