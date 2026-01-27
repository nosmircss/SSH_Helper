using System.Text;
using Rebex.TerminalEmulation;

namespace SSH_Helper.Services
{
    /// <summary>
    /// Creates terminal options with consistent encoding.
    /// </summary>
    public static class SshTerminalOptionsFactory
    {
        public static TerminalOptions Create()
        {
            return new TerminalOptions { Encoding = Encoding.UTF8 };
        }
    }
}
