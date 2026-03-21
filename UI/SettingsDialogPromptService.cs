using SSH_Helper.UI;

namespace SSH_Helper
{
    internal interface ISettingsDialogPromptService
    {
        DialogResult Show(IWin32Window? owner, string message, string title, MessageBoxButtons buttons, MessageBoxIcon icon);
    }

    internal sealed class SettingsDialogPromptService : ISettingsDialogPromptService
    {
        public DialogResult Show(IWin32Window? owner, string message, string title, MessageBoxButtons buttons, MessageBoxIcon icon)
        {
            return DialogTheme.Show(owner, message, title, buttons, icon);
        }
    }
}
