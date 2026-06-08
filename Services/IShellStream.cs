using Rebex.Net;
using Rebex.TerminalEmulation;

// Alias to avoid conflict with SSH_Helper.Services.Scripting namespace
using RebexScripting = Rebex.TerminalEmulation.Scripting;

namespace SSH_Helper.Services
{
    /// <summary>
    /// Minimal seam over the Rebex scripting stream used by the command read loop.
    /// The loop never matches patterns through the stream itself (it accumulates raw
    /// data and runs the prompt regex in managed code), so the only primitive needed
    /// is "read whatever data is currently available." Abstracting it lets the read
    /// loop be unit-tested without a live SSH connection or Rebex license.
    /// </summary>
    internal interface IShellStream
    {
        /// <summary>Read/write timeout in milliseconds for the next <see cref="ReadData"/> call.</summary>
        int Timeout { get; set; }

        /// <summary>Whether the underlying transport is still connected.</summary>
        bool IsConnected { get; }

        /// <summary>
        /// Reads any data currently available on the stream, blocking up to <see cref="Timeout"/>
        /// milliseconds. Throws a timeout exception (message containing "timeout"/"timed out"/
        /// "time limit") when no data arrives within the window — matching Rebex's behaviour.
        /// </summary>
        string ReadData();

        /// <summary>Sends raw data to the stream.</summary>
        void Send(string data);

        /// <summary>Sends an SSH keepalive packet.</summary>
        void KeepAlive();
    }

    /// <summary>
    /// Production <see cref="IShellStream"/> backed by the live Rebex SSH client and
    /// scripting stream. Forwards every call 1:1 so behaviour is identical to talking
    /// to <c>Scripting</c> directly.
    /// </summary>
    internal sealed class RebexShellStream : IShellStream
    {
        private readonly Ssh _ssh;
        private readonly RebexScripting _scripting;
        private readonly ScriptEvent _anyData = ScriptEvent.FromRegex(@"[\s\S]");

        public RebexShellStream(Ssh ssh, RebexScripting scripting)
        {
            _ssh = ssh ?? throw new ArgumentNullException(nameof(ssh));
            _scripting = scripting ?? throw new ArgumentNullException(nameof(scripting));
        }

        public int Timeout
        {
            get => _scripting.Timeout;
            set => _scripting.Timeout = value;
        }

        public bool IsConnected => _ssh.IsConnected;

        public string ReadData() => _scripting.ReadUntil(_anyData);

        public void Send(string data) => _scripting.Send(data);

        public void KeepAlive() => _scripting.KeepAlive();
    }
}
