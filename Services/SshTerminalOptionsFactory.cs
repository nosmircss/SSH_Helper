using System.Text;
using Rebex.Net;
using Rebex.TerminalEmulation;
using RebexScripting = Rebex.TerminalEmulation.Scripting;

namespace SSH_Helper.Services
{
    /// <summary>
    /// Creates terminal options with consistent encoding.
    /// </summary>
    public static class SshTerminalOptionsFactory
    {
        public const int DefaultColumns = 120;
        public const int DefaultRows = 36;
        // Keep scrollback bounded: very large limits can consume gigabytes in VirtualTerminal buffers.
        public const int DefaultHistoryMaxLength = 20000;

        public static TerminalOptions Create()
        {
            return new TerminalOptions { Encoding = Encoding.UTF8 };
        }

        public static (RebexScripting Scripting, VirtualTerminal Terminal) CreateScriptingWithHistory(
            Ssh client,
            TerminalOptions? options = null,
            int columns = DefaultColumns,
            int rows = DefaultRows,
            int historyMaxLength = DefaultHistoryMaxLength)
        {
            ArgumentNullException.ThrowIfNull(client);
            options ??= Create();

            var terminal = new VirtualTerminal(columns, rows, historyMaxLength)
            {
                Options = options
            };
            terminal.Bind(client);
            return (terminal.Scripting, terminal);
        }
    }
}
