using Microsoft.Toolkit.Uwp.Notifications;
using SSH_Helper.Models;

namespace SSH_Helper.Services.Notifications
{
    /// <summary>
    /// Displays Windows 10/11 toast notifications via Microsoft.Toolkit.Uwp.Notifications.
    /// Requires no profile — toasts are app-local. Unpackaged apps without a registered shortcut
    /// will still show toasts but cannot handle click activation.
    /// </summary>
    public class ToastDispatcher
    {
        public virtual Task<NotificationResult> SendAsync(
            string? title,
            string message,
            NotificationLevel level,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var builder = new ToastContentBuilder();
                if (!string.IsNullOrWhiteSpace(title))
                    builder.AddText(title);
                builder.AddText(message);

                var attribution = level switch
                {
                    NotificationLevel.Warn => "Warning",
                    NotificationLevel.Error => "Error",
                    NotificationLevel.Success => "Success",
                    _ => "Info"
                };
                builder.AddAttributionText(attribution);

                builder.Show();
                return Task.FromResult(NotificationResult.Success("toast"));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return Task.FromResult(NotificationResult.Failure("toast", ex.Message));
            }
        }
    }
}
